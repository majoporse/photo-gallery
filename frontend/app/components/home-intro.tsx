import { useLayoutEffect, useRef, type ReactNode, type RefObject } from "react";
import {
  motion,
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

const FRAME_WIDTH_VW = 70;
const TRACK_HEIGHT_VH = 700;
/** 120% wide image, 10% bleed each side. */
const IMAGE_WIDTH_PCT = 120;
const IMAGE_LEFT_PCT = -10;
/**
 * Per-frame parallax in vw, varied by position so the row does not feel
 * mechanical. Kept below the 7vw of bleed (10% of a 70vw frame).
 */
const PARALLAX_VW = [2.8, 4.6, 3.4, 6, 3, 5.4, 3.8, 6.4, 3.2, 5] as const;

interface HomeIntroProps {
  photos: readonly PhotoSummary[];
}

/**
 * Scroll-driven opening sequence. The server and the first client render both
 * see progress 0 (no window or media query is read during render), so hydration
 * is stable. Reduced motion is applied after mount for the same reason: motion
 * is confirmed on the client, then the short static version replaces the track.
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
  const { scrollYProgress } = useScroll({
    target: trackRef,
    offset: ["start start", "end end"],
  });

  // 700vw of frames minus one viewport of travel; the last frame finishes
  // mostly in view while the previous frame keeps the left edge filled.
  const travelVw = Math.max(0, photos.length * FRAME_WIDTH_VW - 100);
  const rowX = useTransform(scrollYProgress, [0, 1], ["0vw", `-${travelVw}vw`]);
  const cueOpacity = useTransform(scrollYProgress, [0, 0.015], [1, 0]);
  const nameVisibility = useTransform(scrollYProgress, (value) =>
    value >= 0.999 ? "hidden" : "visible",
  );

  return (
    <section
      aria-labelledby="intro-name"
      ref={trackRef}
      className="relative bg-fg"
      style={{ height: `${TRACK_HEIGHT_VH}vh` }}
    >
      <div className="sticky top-0 h-dvh overflow-hidden">
        <motion.div
          aria-hidden="true"
          className="absolute inset-0 flex gap-2 will-change-transform"
          style={{ x: rowX }}
        >
          {photos.map((photo, index) => (
            <IntroFrame
              key={photo.id}
              photo={photo}
              imageBaseUrl={imageBaseUrl}
              index={index}
              progress={scrollYProgress}
            />
          ))}
        </motion.div>
      </div>

      <motion.div
        style={{ visibility: nameVisibility }}
        className="pointer-events-none fixed inset-x-0 bottom-0 z-20 border-t border-bg/20 bg-fg/90 px-5 py-4 sm:px-8 sm:py-6"
      >
        <NamePlate
          cue={
            <motion.span
              aria-hidden="true"
              style={{ opacity: cueOpacity }}
              className="shrink-0 pb-1 text-[0.6rem] tracking-[0.3em] text-bg/70 uppercase"
            >
              Scroll
            </motion.span>
          }
        />
      </motion.div>
    </section>
  );
}

function IntroFrame({
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
  const isFirst = index === 0;
  const revealRef = useRef<HTMLDivElement>(null);
  const lineRef = useRef<HTMLSpanElement>(null);
  const parallaxVw = PARALLAX_VW[index % PARALLAX_VW.length] ?? 4;
  const parallax = useTransform(
    progress,
    [0, 1],
    ["0vw", `${parallaxVw}vw`],
  );

  useCrtReveal(revealRef, lineRef, isFirst, photo.id);

  return (
    <div
      data-intro-slide
      className="relative h-full w-[70vw] shrink-0 overflow-hidden"
    >
      <div ref={revealRef} className="absolute inset-0">
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
            sizes="84vw"
            loading={isFirst ? "eager" : "lazy"}
            fetchPriority={isFirst ? "high" : "auto"}
            alt=""
            imgClassName="h-full w-full object-cover"
          />
        </motion.div>
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 z-10 border border-rule"
        />
      </div>

      {isFirst ? (
        <span
          ref={lineRef}
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 top-1/2 z-10 h-px -translate-y-1/2 bg-bg opacity-0 shadow-[0_0_24px_4px_var(--bg)]"
        />
      ) : null}
    </div>
  );
}

/**
 * One-shot CRT turn-on for the first frame. The server ships the full frame;
 * this collapses it in a layout effect (before paint for a hydrating client)
 * and opens it vertically from a bright line. No-js and crawlers keep the
 * photograph, and reduced-motion clients skip the reveal entirely.
 */
function useCrtReveal(
  revealRef: RefObject<HTMLDivElement | null>,
  lineRef: RefObject<HTMLSpanElement | null>,
  enabled: boolean,
  photoId: string,
) {
  useLayoutEffect(() => {
    if (!enabled) {
      return;
    }

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
  }, [enabled, photoId, revealRef, lineRef]);
}

function NamePlate({ cue }: { cue?: ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-6">
      <div className="min-w-0">
        <p
          id="intro-name"
          className="font-heading text-[clamp(2.75rem,8vw,7rem)] leading-[0.88] tracking-[-0.045em] text-balance text-bg"
        >
          {site.name}
        </p>
        {site.tagline ? (
          <p className="mt-3 max-w-md text-sm leading-snug text-bg/75">
            {site.tagline}
          </p>
        ) : null}
      </div>
      {cue}
    </div>
  );
}

function StaticIntro({ photo }: { photo: PhotoSummary }) {
  const { imageBaseUrl } = useRootData();

  return (
    <section
      aria-labelledby="intro-name"
      className="relative h-dvh overflow-hidden bg-fg"
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

      {/* The header offsets this section, so the plate sits 7rem up to stay
          inside the viewport on a short mobile screen. */}
      <div className="absolute inset-x-0 bottom-28 border-t border-bg/20 bg-fg/90 px-5 py-4 sm:px-8 sm:py-6">
        <NamePlate />
      </div>
    </section>
  );
}
