import {
  useEffect,
  useLayoutEffect,
  useRef,
  type RefObject,
} from "react";
import {
  motion,
  useMotionValueEvent,
  useReducedMotion,
  useScroll,
  useTransform,
  type MotionValue,
} from "motion/react";
import type { PhotoSummary } from "~/api/generated/model";
import { PhotoMedia } from "~/components/photo-media";
import { site } from "~/content/site";
import { useRootData } from "~/lib/root-data";
import { useMounted } from "~/lib/use-mounted";

/**
 * Storyboard (progress over the 1100vh track, 0 at the top, 1 at the end):
 *
 *   reveal   0.00–0.04  mount-time CRT; scroll is held at the full frame
 *   shrink   0.04–0.22  frame 0: 100vw/100dvh -> 46vw/62dvh (layout width/height)
 *   travel   0.22–0.78  the row translates -360vw, parallax per frame
 *   name     0.34–0.52  hard wipe in (3x the old window), then held to 1.00
 *   header   0.94–1.00  fixed header slides down with the scroll progress
 *
 * The 1100vh track gives 1000vh of scroll after the sticky stage is subtracted:
 * 40vh reveal hold, 180vh shrink, 560vh travel, 180vh name wipe (inside the
 * travel), 160vh final hold, and 60vh header. No image or name opacity is
 * animated. Only the name boundary moved; every other phase boundary is
 * unchanged in progress space.
 */
const TRACK_HEIGHT_VH = 1100;
const SHRINK: [number, number] = [0.04, 0.22];
const TRAVEL: [number, number] = [0.22, 0.78];
const NAME_REVEAL: [number, number] = [0.34, 0.52];
const HEADER_START = 0.94;
const HOVER_ZONE_PX = 72;

const FRAME_VW = 46;
const FRAME_VH = 62;
const FRAME_GAP_PX = 24;
const GUTTER_PX = 10;
const FRAME_INSET_PX = GUTTER_PX + 1;
const IMAGE_WIDTH_PCT = 132;
const IMAGE_LEFT_PCT = -16;
const FRAME_IMAGE_VW = Math.round((FRAME_VW * IMAGE_WIDTH_PCT) / 100);
/**
 * Per-frame parallax in vw, varied by position. Max 7.0vw against 7.36vw of
 * bleed per side (16% of a 46vw frame), so the image edge never leaves the
 * frame. This is the capped maximum; do not raise it past 7.3.
 */
const PARALLAX_VW = [3.5, 5.2, 4.1, 6.6, 3.8, 5.8, 4.6, 7, 3.9, 6.2] as const;

/** 10px of `--bg` gutter, then a 1px `--rule` hairline, painted outside the box. */
const FRAME_SHADOW = `0 0 0 ${GUTTER_PX}px var(--bg), 0 0 0 ${GUTTER_PX + 1}px var(--rule)`;

interface HomeIntroProps {
  photos: readonly PhotoSummary[];
}

/**
 * Server and first client render both start at progress 0 with no media query
 * read during render, so hydration is stable. Reduced motion is confirmed after
 * mount and swaps in the static composition.
 */
export function HomeIntro({ photos }: HomeIntroProps) {
  const reducedMotion = useReducedMotion();
  const mounted = useMounted();
  const first = photos[0];

  if (!first) {
    return null;
  }

  if (mounted && reducedMotion) {
    return <StaticIntro photo={first} />;
  }

  return <ScrollingIntro photos={photos} />;
}

function ScrollingIntro({ photos }: HomeIntroProps) {
  const { imageBaseUrl } = useRootData();
  const trackRef = useRef<HTMLElement>(null);
  const hoveringRef = useRef(false);
  const { scrollYProgress } = useScroll({
    target: trackRef,
    offset: ["start start", "end end"],
  });

  const travelVw = Math.max(0, photos.length * FRAME_VW - 100);
  const rowX = useTransform(scrollYProgress, TRAVEL, ["0vw", `-${travelVw}vw`]);
  const cueOpacity = useTransform(scrollYProgress, [0, 0.02], [1, 0]);
  const nameClip = useTransform(
    scrollYProgress,
    NAME_REVEAL,
    ["inset(0% 100% 0% 0%)", "inset(0% 0% 0% 0%)"],
  );

  useLayoutEffect(() => {
    syncIntroChrome(scrollYProgress.get(), hoveringRef.current);
    return () => {
      document.documentElement.style.removeProperty("--intro-header-y");
    };
  }, [scrollYProgress]);

  useMotionValueEvent(scrollYProgress, "change", (value) => {
    syncIntroChrome(value, hoveringRef.current);
  });

  useEffect(() => {
    const onPointerMove = (event: PointerEvent) => {
      if (event.pointerType !== "mouse" && event.pointerType !== "pen") {
        return;
      }

      const inside = event.clientY >= 0 && event.clientY <= HOVER_ZONE_PX;
      if (inside === hoveringRef.current) {
        return;
      }

      hoveringRef.current = inside;
      const root = document.documentElement;
      if (inside) {
        root.dataset.intro = "hover";
      } else {
        root.dataset.intro =
          scrollYProgress.get() >= HEADER_START ? "done" : "playing";
      }
    };

    const onPointerLeave = () => {
      if (!hoveringRef.current) {
        return;
      }

      hoveringRef.current = false;
      document.documentElement.dataset.intro =
        scrollYProgress.get() >= HEADER_START ? "done" : "playing";
    };

    window.addEventListener("pointermove", onPointerMove, { passive: true });
    document.documentElement.addEventListener("pointerleave", onPointerLeave);

    return () => {
      window.removeEventListener("pointermove", onPointerMove);
      document.documentElement.removeEventListener(
        "pointerleave",
        onPointerLeave,
      );
    };
  }, [scrollYProgress]);

  return (
    <section
      aria-labelledby="intro-name"
      ref={trackRef}
      className="relative bg-bg"
      style={{ height: `${TRACK_HEIGHT_VH}vh` }}
    >
      <div className="sticky top-0 h-dvh overflow-hidden bg-bg">
        <motion.div
          aria-hidden="true"
          className="absolute inset-0 flex items-center will-change-transform"
          style={{ x: rowX, gap: FRAME_GAP_PX, paddingLeft: FRAME_INSET_PX }}
        >
          {photos[0] ? (
            <FirstFrame
              photo={photos[0]}
              imageBaseUrl={imageBaseUrl}
              progress={scrollYProgress}
            />
          ) : null}
          {photos.slice(1).map((photo, offset) => (
            <Frame
              key={photo.id}
              photo={photo}
              imageBaseUrl={imageBaseUrl}
              index={offset + 1}
              progress={scrollYProgress}
            />
          ))}
        </motion.div>

        <motion.div className="pointer-events-none absolute inset-0 z-20 flex items-center justify-center px-5">
          <motion.div style={{ clipPath: nameClip }} className="w-fit">
            <NameCard />
          </motion.div>
        </motion.div>

        <motion.div
          aria-hidden="true"
          style={{ opacity: cueOpacity }}
          className="pointer-events-none absolute inset-x-0 bottom-6 z-20 flex justify-center"
        >
          <span className="intro-cue border border-rule bg-bg px-3 py-1.5 text-[0.6rem] tracking-[0.3em] text-fg uppercase">
            Scroll
          </span>
        </motion.div>
      </div>
    </section>
  );
}

/**
 * The full-bleed opener. Only width/height animate; the 10px/1px frame edge is
 * a box-shadow, so the shrink costs layout on this one element and never thins
 * the border the way a scale transform would.
 */
function FirstFrame({
  photo,
  imageBaseUrl,
  progress,
}: {
  photo: PhotoSummary;
  imageBaseUrl: string;
  progress: MotionValue<number>;
}) {
  const revealRef = useRef<HTMLDivElement>(null);
  const lineRef = useRef<HTMLSpanElement>(null);
  const width = useTransform(progress, SHRINK, ["100vw", `${FRAME_VW}vw`]);
  const height = useTransform(progress, SHRINK, ["100dvh", `${FRAME_VH}dvh`]);
  // The row's 11px inset is cancelled at full-bleed, so frame 0 starts at x=0
  // and lands 11px in: the 10px gutter and 1px rule appear on its left too.
  const marginLeft = useTransform(progress, SHRINK, [-FRAME_INSET_PX, 0]);
  const parallax = useParallax(progress, 0);

  useCrtReveal(revealRef, lineRef, photo.id);

  return (
    <motion.div
      data-intro-slide
      className="relative shrink-0"
      style={{ width, height, marginLeft, boxShadow: FRAME_SHADOW }}
    >
      <div ref={revealRef} className="absolute inset-0 overflow-hidden">
        <FrameImage
          photo={photo}
          imageBaseUrl={imageBaseUrl}
          parallax={parallax}
          sizes="100vw"
          priority
        />
      </div>
      <span
        ref={lineRef}
        aria-hidden="true"
        className="pointer-events-none absolute inset-x-0 top-1/2 z-10 h-px -translate-y-1/2 bg-bg opacity-0 shadow-[0_0_24px_4px_var(--bg)]"
      />
    </motion.div>
  );
}

function Frame({
  photo,
  imageBaseUrl,
  index,
  progress,
}: {
  photo: PhotoSummary;
  imageBaseUrl: string;
  index: number;
  progress: MotionValue<number>;
}) {
  const parallax = useParallax(progress, index);

  return (
    <div
      data-intro-slide
      className="relative shrink-0"
      style={{
        width: `${FRAME_VW}vw`,
        height: `${FRAME_VH}dvh`,
        boxShadow: FRAME_SHADOW,
      }}
    >
      <div className="absolute inset-0 overflow-hidden">
        <FrameImage
          photo={photo}
          imageBaseUrl={imageBaseUrl}
          parallax={parallax}
          sizes={`${FRAME_IMAGE_VW}vw`}
        />
      </div>
    </div>
  );
}

function FrameImage({
  photo,
  imageBaseUrl,
  parallax,
  sizes,
  priority = false,
}: {
  photo: PhotoSummary;
  imageBaseUrl: string;
  parallax: MotionValue<string>;
  sizes: string;
  priority?: boolean;
}) {
  return (
    <motion.div
      className="absolute inset-y-0 will-change-transform"
      style={{
        x: parallax,
        left: `${IMAGE_LEFT_PCT}%`,
        width: `${IMAGE_WIDTH_PCT}%`,
      }}
    >
      <PhotoMedia
        photo={photo}
        imageBaseUrl={imageBaseUrl}
        sizes={sizes}
        loading={priority ? "eager" : "lazy"}
        fetchPriority={priority ? "high" : "auto"}
        alt=""
        imgClassName="h-full w-full object-cover"
      />
    </motion.div>
  );
}

function useParallax(
  progress: MotionValue<number>,
  index: number,
): MotionValue<string> {
  const magnitude = PARALLAX_VW[index % PARALLAX_VW.length] ?? 2;
  return useTransform(progress, [0, 1], ["0vw", `${magnitude}vw`]);
}

/**
 * One-shot CRT turn-on. The server ships the full frame; this collapses it in a
 * layout effect (before the hydrated paint) and opens it vertically from a
 * bright line. No-js and crawlers keep the photograph, and reduced-motion
 * clients skip the reveal entirely.
 */
function useCrtReveal(
  revealRef: RefObject<HTMLDivElement | null>,
  lineRef: RefObject<HTMLSpanElement | null>,
  photoId: string,
) {
  useLayoutEffect(() => {
    const reveal = revealRef.current;
    if (!reveal) {
      return;
    }

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      return;
    }

    const line = lineRef.current;
    const collapsed = "inset(49.86% 0 49.86% 0)";

    reveal.style.clipPath = collapsed;
    reveal.style.filter = "brightness(2.6) contrast(1.06)";
    if (line) {
      line.style.opacity = "1";
    }

    const opening = reveal.animate(
      [
        { clipPath: collapsed, filter: "brightness(2.6) contrast(1.06)" },
        {
          clipPath: "inset(30% 0 30% 0)",
          filter: "brightness(1.9) contrast(1.03)",
          offset: 0.35,
        },
        { clipPath: "inset(0% 0 0% 0)", filter: "brightness(1) contrast(1)" },
      ],
      {
        duration: 1000,
        delay: 100,
        easing: "cubic-bezier(0.33, 1, 0.68, 1)",
        fill: "both",
      },
    );

    const glow = line?.animate(
      [
        { opacity: 1, transform: "scaleX(0.86)" },
        { opacity: 1, transform: "scaleX(1)", offset: 0.2 },
        { opacity: 0, transform: "scaleX(1) scaleY(0.35)" },
      ],
      { duration: 700, easing: "ease-out", fill: "both" },
    );

    return () => {
      opening.cancel();
      glow?.cancel();
      reveal.style.clipPath = "";
      reveal.style.filter = "";
      if (line) {
        line.style.opacity = "";
      }
    };
  }, [photoId, revealRef, lineRef]);
}

function NameCard() {
  return (
    <div className="border border-rule bg-bg px-6 py-4 sm:px-10 sm:py-6">
      <p
        id="intro-name"
        className="text-center font-heading text-[clamp(3rem,10vw,8rem)] leading-[0.85] tracking-[-0.045em] text-fg"
      >
        {site.name}
      </p>
      {site.tagline ? (
        <p className="mt-3 text-center text-sm text-muted-foreground">
          {site.tagline}
        </p>
      ) : null}
    </div>
  );
}

function StaticIntro({ photo }: { photo: PhotoSummary }) {
  const { imageBaseUrl } = useRootData();

  useLayoutEffect(() => {
    const root = document.documentElement;
    root.dataset.intro = "done";
    root.style.setProperty("--intro-header-y", "0%");
  }, []);

  return (
    <section
      aria-labelledby="intro-name"
      className="relative h-dvh overflow-hidden bg-bg"
    >
      <div aria-hidden="true" className="absolute inset-0">
        <PhotoMedia
          photo={photo}
          imageBaseUrl={imageBaseUrl}
          sizes="100vw"
          loading="eager"
          fetchPriority="high"
          alt=""
          imgClassName="h-full w-full object-cover"
        />
      </div>

      <div className="pointer-events-none absolute inset-0 z-20 flex items-center justify-center px-5">
        <NameCard />
      </div>
    </section>
  );
}

/**
 * Header is out of flow on `/`: the CSS var is its scroll-linked translation
 * (-100% before the header phase, 0% at the end). `data-intro` flips to done at
 * 0.94 so the fixed header becomes visible while the var slides it down.
 */
function syncIntroChrome(progress: number, hovering: boolean): void {
  const root = document.documentElement;
  const phase = Math.min(
    1,
    Math.max(0, (progress - HEADER_START) / (1 - HEADER_START)),
  );
  root.style.setProperty("--intro-header-y", `${-100 + phase * 100}%`);

  if (!hovering) {
    root.dataset.intro = progress >= HEADER_START ? "done" : "playing";
  }
}
