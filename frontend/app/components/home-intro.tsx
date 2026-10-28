import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type RefObject,
} from "react";
import {
  motion,
  useMotionValue,
  useMotionValueEvent,
  useReducedMotion,
  useScroll,
  useSpring,
  useTransform,
  type MotionValue,
} from "motion/react";
import type { PhotoSummary } from "~/api/generated/model";
import { PhotoMedia } from "~/components/photo-media";
import { site } from "~/content/site";
import { useRootData } from "~/lib/root-data";
import { useIntroDrag } from "~/lib/use-intro-drag";
import { useMounted } from "~/lib/use-mounted";

/**
 * Storyboard (progress over the 1000vh track, 0 at the top, 1 at the end):
 *
 *   reveal   0.00–0.04  scroll-linked CRT: frame 0 is fully collapsed at the top
 *                       of the track and opens from the centre with a bright
 *                       flash, so scrolling back to the top blanks the stage
 *   shrink   0.04–0.22  the whole row scales from cover size (frame 0 still
 *                       covers the viewport at its own ratio) down to 62dvh
 *                       frames, so every photograph shares one zoom level
 *   travel   0.22–1.00  the row moves until the last frame is flush with the
 *                       viewport's right edge, parallax per frame
 *   name     0.34–0.52  hard wipe in, then held to 1.00
 *   header   0.94–1.00  fixed header slides down with the scroll progress
 *
 * The 1000vh track gives 900vh of scroll after the sticky stage is subtracted:
 * 36vh reveal, 162vh shrink, then 702vh travel. The name wipe and the header
 * phase both sit inside the travel, so there is no dead stretch after the last
 * photograph lands. No image or name opacity is animated.
 *
 * Native scrolling drives `useScroll`; every visual reads a spring-smoothed
 * copy of that progress, so the motion eases without intercepting wheel,
 * keyboard, touch or the scrollbar. Mouse and pen drags write the window scroll
 * position directly (see useIntroDrag), with the same spring easing the settle.
 */
const TRACK_HEIGHT_VH = 1000;
const REVEAL: [number, number] = [0, 0.04];
const REVEAL_FLASH_AT = 0.014;
const SHRINK: [number, number] = [0.04, 0.22];
const TRAVEL: [number, number] = [0.22, 1];
const NAME_REVEAL: [number, number] = [0.34, 0.52];
const HEADER_START = 0.94;
const HOVER_ZONE_PX = 72;

/** Matches the old scroll spring; the visual values ease behind native scroll. */
const SPRING = {
  stiffness: 300,
  damping: 38,
  mass: 0.8,
  restDelta: 0.0001,
} as const;

const FRAME_HEIGHT_VH = 62;
const FRAME_GAP_PX = 24;
const GUTTER_PX = 10;
const FRAME_INSET_PX = GUTTER_PX;
/**
 * Per-frame parallax as a percentage of the frame width. Each image runs from
 * -m% to +m% across the travel window, so the visible crop travels 2m.
 * Raise or lower these freely; the image over-scale below follows the largest.
 */
const PARALLAX_PCT = [33, 19.5, 15, 21, 12, 17.5, 16, 21, 13.5, 19.5] as const;

/**
 * The image needs at least `m` of bleed on each side for the widest parallax,
 * so its width is the frame width plus both bleeds. Two extra points absorb
 * subpixel rounding. `useParallax` converts the frame-relative travel back to
 * a percentage of the image's own width.
 */
const PARALLAX_BLEED_PCT = Math.max(...PARALLAX_PCT) + 2;
const IMAGE_WIDTH_PCT = 100 + PARALLAX_BLEED_PCT * 2;
const IMAGE_LEFT_PCT = -PARALLAX_BLEED_PCT;

/** 10px of `--bg` gutter painted outside the box; no hairline outline. */
const FRAME_SHADOW = `0 0 0 ${GUTTER_PX}px var(--bg)`;

/** Width that gives the photo's ratio at the shared frame height. */
function frameWidth(aspect: number): string {
  return `${(FRAME_HEIGHT_VH * aspect).toFixed(3)}dvh`;
}

/** Width the over-scaled image occupies at the shared frame height. */
function imageWidth(aspect: number): string {
  return `${((FRAME_HEIGHT_VH * aspect * IMAGE_WIDTH_PCT) / 100).toFixed(3)}dvh`;
}


function photoAspect(photo: PhotoSummary): number {
  const width = Number(photo.width);
  const height = Number(photo.height);
  if (
    !Number.isFinite(width) ||
    !Number.isFinite(height) ||
    width <= 0 ||
    height <= 0
  ) {
    return 1.5;
  }
  return width / height;
}

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
  const stageRef = useRef<HTMLDivElement>(null);
  const hoveringRef = useRef(false);
  const { scrollYProgress } = useScroll({
    target: trackRef,
    offset: ["start start", "end end"],
  });
  // Native scroll stays in charge; only the visual progress is damped, so
  // trackpad inertia, keyboard, touch and the scrollbar all keep working.
  const progress = useSpring(scrollYProgress, {
    ...SPRING,
    skipInitialAnimation: true,
  });
  const openerAspect = photos[0] ? photoAspect(photos[0]) : 1.5;
  const { scale, ready } = useIntroScale(progress, openerAspect, stageRef);
  const drag = useIntroDrag(trackRef);

  const rowX = useTransform(progress, TRAVEL, [
    "calc(0% + 0vw)",
    "calc(-100% + 100vw)",
  ]);
  const cueOpacity = useTransform(progress, [0, 0.02], [1, 0]);
  const nameClip = useTransform(
    progress,
    NAME_REVEAL,
    ["inset(0% 100% 0% 0%)", "inset(0% 0% 0% 0%)"],
  );

  useLayoutEffect(() => {
    syncIntroChrome(progress.get(), hoveringRef.current);
    return () => {
      document.documentElement.style.removeProperty("--intro-header-y");
    };
  }, [progress]);

  useMotionValueEvent(progress, "change", (value) => {
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
          progress.get() >= HEADER_START ? "done" : "playing";
      }
    };

    const onPointerLeave = () => {
      if (!hoveringRef.current) {
        return;
      }

      hoveringRef.current = false;
      document.documentElement.dataset.intro =
        progress.get() >= HEADER_START ? "done" : "playing";
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
  }, [progress]);

  return (
    <section
      aria-labelledby="intro-name"
      ref={trackRef}
      className="relative bg-bg"
      style={{ height: `${TRACK_HEIGHT_VH}vh` }}
    >
      <div
        ref={stageRef}
        className="sticky top-0 h-dvh cursor-grab overflow-hidden bg-bg active:cursor-grabbing"
        {...drag}
      >
        <motion.div
          aria-hidden="true"
          className="absolute inset-y-0 left-0 flex w-max items-center will-change-transform"
          style={{
            x: rowX,
            scale,
            transformOrigin: "left center",
            gap: FRAME_GAP_PX,
            paddingLeft: FRAME_INSET_PX,
            // The server cannot know the viewport, so the first client paint
            // hides the row until the cover scale has been measured.
            visibility: ready ? "visible" : "hidden",
          }}
        >
          {photos[0] ? (
            <FirstFrame
              photo={photos[0]}
              imageBaseUrl={imageBaseUrl}
              progress={progress}
            />
          ) : null}
          {photos.slice(1).map((photo, offset) => (
            <Frame
              key={photo.id}
              photo={photo}
              imageBaseUrl={imageBaseUrl}
              index={offset + 1}
              progress={progress}
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
          className="pointer-events-none absolute inset-0 z-20 flex items-center justify-center"
        >
          <span className="intro-cue flex flex-col items-center gap-6">
            <span className="text-center text-xl tracking-[0.45em] text-muted-foreground uppercase sm:text-2xl">
              Scroll
            </span>
            <span aria-hidden="true" className="h-28 w-px bg-rule" />
            <span
              aria-hidden="true"
              className="h-3 w-3 rotate-45 border-r border-b border-fg"
            />
          </span>
        </motion.div>
      </div>
    </section>
  );
}

/**
 * The full-bleed opener. It is laid out at the shared frame size and the row's
 * scale makes it cover the viewport at progress 0, so it stays in step with the
 * other nine frames throughout the shrink. The CRT reveal is scroll-linked
 * here, so the photograph is fully hidden at progress 0 and opens from the
 * centre.
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
  const aspect = photoAspect(photo);
  // The row's 10px inset is cancelled at full-bleed, so frame 0 starts at x=0
  // and lands 10px in: the gutter appears on its left too. Scaling the row
  // scales the inset with it, so the cancellation still lands on zero.
  const marginLeft = useTransform(progress, SHRINK, [-FRAME_INSET_PX, 0]);
  const parallax = useParallax(progress, 0);
  const revealStops = [REVEAL[0], REVEAL_FLASH_AT, REVEAL[1]];
  const revealClip = useTransform(progress, revealStops, [
    "inset(50% 0 50% 0)",
    "inset(30% 0 30% 0)",
    "inset(0% 0 0% 0)",
  ]);
  const revealFilter = useTransform(progress, revealStops, [
    "brightness(2.6) contrast(1.06)",
    "brightness(1.9) contrast(1.03)",
    "brightness(1) contrast(1)",
  ]);

  return (
    <motion.div
      data-intro-slide
      className="relative shrink-0"
      style={{
        width: frameWidth(aspect),
        height: `${FRAME_HEIGHT_VH}dvh`,
        marginLeft,
        boxShadow: FRAME_SHADOW,
      }}
    >
      <motion.div
        style={{ clipPath: revealClip, filter: revealFilter }}
        className="absolute inset-0 overflow-hidden"
      >
        <FrameImage
          photo={photo}
          imageBaseUrl={imageBaseUrl}
          parallax={parallax}
          sizes="100vw"
          priority
        />
      </motion.div>
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
  const aspect = photoAspect(photo);

  return (
    <div
      data-intro-slide
      className="relative shrink-0"
      style={{
        width: frameWidth(aspect),
        height: `${FRAME_HEIGHT_VH}dvh`,
        boxShadow: FRAME_SHADOW,
      }}
    >
      <div className="absolute inset-0 overflow-hidden">
        <FrameImage
          photo={photo}
          imageBaseUrl={imageBaseUrl}
          parallax={parallax}
          sizes={`max(100vw, ${imageWidth(aspect)})`}
        />
      </div>
    </div>
  );
}

/**
 * One scale for the whole strip. At progress 0 it is the ratio of frame 0's
 * cover width to its shared frame width; by the end of the shrink it is 1.
 * The ratio depends on the viewport, so it is measured from the sticky stage
 * and re-measured on resize. Until that happens the row stays hidden: the
 * server cannot know `100vw` in pixels, and an unscaled row would show nine
 * thumbnails beside the opener for one paint.
 */
function useIntroScale(
  progress: MotionValue<number>,
  openerAspect: number,
  stageRef: RefObject<HTMLDivElement | null>,
): { scale: MotionValue<number>; ready: boolean } {
  const coverScale = useMotionValue(1);
  const [ready, setReady] = useState(false);

  useLayoutEffect(() => {
    const stage = stageRef.current;
    if (!stage) {
      return;
    }

    const measure = () => {
      const width = stage.clientWidth;
      const height = stage.clientHeight;
      if (width <= 0 || height <= 0) {
        return;
      }

      const frameWidth = (FRAME_HEIGHT_VH / 100) * height * openerAspect;
      if (frameWidth <= 0) {
        return;
      }

      coverScale.set(Math.max(width, openerAspect * height) / frameWidth);
      setReady(true);
    };

    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(stage);
    return () => observer.disconnect();
  }, [coverScale, openerAspect, stageRef]);

  const scale = useTransform<number, number>(
    [progress, coverScale],
    (latest) => {
      const value = latest[0] ?? 0;
      const cover = latest[1] ?? 1;
      const t = clamp01((value - SHRINK[0]) / (SHRINK[1] - SHRINK[0]));
      return cover + (1 - cover) * t;
    },
  );

  return { scale, ready };
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
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
  const magnitude = PARALLAX_PCT[index % PARALLAX_PCT.length] ?? 4;
  // x percentages resolve against the over-scaled image, not the frame.
  const shift = (magnitude * 100) / IMAGE_WIDTH_PCT;
  return useTransform(progress, TRAVEL, [`-${shift}%`, `${shift}%`]);
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
