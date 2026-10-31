import { useLayoutEffect, useRef, useState } from "react";
import {
  motion,
  useMotionValue,
  useScroll,
  useTransform,
  type MotionValue,
} from "motion/react";
import type { PhotoSummary } from "~/api/generated/model";
import { PhotoMedia } from "~/components/photo-media";
import { site } from "~/content/site";
import { photoAspect } from "~/lib/photo-aspect";
import { useRootData } from "~/lib/root-data";

/**
 * Phone composition: the desktop strip rotated into a vertical film roll.
 *
 * A 16px paper gate stays on screen while the strip travels one pixel per
 * pixel of scroll, so the film reads as passing through a projector instead
 * of trailing a spring. The opening frame fills the gate and keeps the desktop
 * CRT reveal; the rest keep their real ratio at the gate width. The name stays
 * pinned and wipes in at the same progress as desktop. Per-frame parallax is
 * the desktop one with the axis flipped: images drift inside their frames.
 */
const FRAME_COUNT = 10;
const GUTTER_PX = 16;
const FRAME_GAP_PX = 12;
const REVEAL: [number, number] = [0, 0.08];
const REVEAL_FLASH_AT = 0.028;
const NAME_REVEAL: [number, number] = [0.34, 0.52];
const IMAGE_SIZES = "calc(100vw - 2rem)";

/**
 * Per-frame parallax as a percentage of the frame height. Each image runs from
 * -m% to +m% across the travel window, so the visible crop travels 2m. The
 * image over-scale below follows the largest.
 */
const PARALLAX_PCT = [12, 18, 10, 15, 20, 11, 16, 13, 19, 10] as const;

/**
 * The image needs at least `m` of bleed above and below for the widest
 * parallax, so its height is the frame height plus both bleeds.
 */
const PARALLAX_BLEED_PCT = Math.max(...PARALLAX_PCT) + 2;
const IMAGE_HEIGHT_PCT = 100 + PARALLAX_BLEED_PCT * 2;
const IMAGE_TOP_PCT = -PARALLAX_BLEED_PCT;

/** Parallax starts once the opener has finished revealing. */
const TRAVEL: [number, number] = [REVEAL[1], 1];

interface MobileIntroProps {
  photos: readonly PhotoSummary[];
}

export function MobileIntro({ photos }: MobileIntroProps) {
  const frames = photos.slice(0, FRAME_COUNT);
  const { imageBaseUrl } = useRootData();
  const trackRef = useRef<HTMLElement>(null);
  const stageRef = useRef<HTMLDivElement>(null);
  const stripRef = useRef<HTMLDivElement>(null);
  const [stripHeight, setStripHeight] = useState(0);
  const [stageHeight, setStageHeight] = useState(0);

  const { scrollYProgress } = useScroll({
    target: trackRef,
    offset: ["start start", "end end"],
  });

  useLayoutEffect(() => {
    const strip = stripRef.current;
    const stage = stageRef.current;
    if (!strip || !stage) {
      return;
    }

    const measure = () => {
      setStripHeight(strip.offsetHeight);
      setStageHeight(stage.clientHeight);
    };

    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(strip);
    observer.observe(stage);
    return () => observer.disconnect();
  }, []);

  const gateHeight = Math.max(0, stageHeight - GUTTER_PX * 2);
  const maxShift = useMotionValue(0);

  useLayoutEffect(() => {
    maxShift.set(Math.max(0, stripHeight - gateHeight));
  }, [gateHeight, maxShift, stripHeight]);

  // Both values are read reactively, so a resize re-maps the same 0..1 scroll.
  const y = useTransform(() => -scrollYProgress.get() * maxShift.get());
  const nameClip = useTransform(
    scrollYProgress,
    NAME_REVEAL,
    ["inset(0% 100% 0% 0%)", "inset(0% 0% 0% 0%)"],
  );
  const cueOpacity = useTransform(scrollYProgress, [0, 0.02], [1, 0]);

  const trackHeight =
    stripHeight > 0 && stageHeight > 0
      ? Math.max(stripHeight + GUTTER_PX * 2, stageHeight)
      : "100dvh";

  return (
    <section
      aria-labelledby="intro-name"
      ref={trackRef}
      className="relative bg-bg"
      style={{ height: trackHeight }}
    >
      <div
        ref={stageRef}
        className="sticky top-0 h-dvh overflow-hidden bg-bg p-4"
      >
        <div className="relative h-full overflow-hidden">
          <motion.div
            ref={stripRef}
            className="absolute inset-x-0 top-0 flex flex-col will-change-transform"
            style={{ y, gap: FRAME_GAP_PX }}
          >
            {frames[0] ? (
              <MobileOpener
                photo={frames[0]}
                imageBaseUrl={imageBaseUrl}
                progress={scrollYProgress}
              />
            ) : null}
            {frames.slice(1).map((photo, index) => (
              <MobileFrame
                key={photo.id}
                photo={photo}
                imageBaseUrl={imageBaseUrl}
                index={index + 1}
                progress={scrollYProgress}
              />
            ))}
          </motion.div>
        </div>

        <motion.div className="pointer-events-none absolute inset-0 z-20 flex items-center justify-center px-4">
          <motion.div style={{ clipPath: nameClip }} className="w-fit">
            <MobileNameCard />
          </motion.div>
        </motion.div>

        <motion.div
          aria-hidden="true"
          style={{ opacity: cueOpacity }}
          className="pointer-events-none absolute inset-0 z-20 flex items-center justify-center"
        >
          <span className="intro-cue flex flex-col items-center gap-6">
            <span className="text-center text-xl tracking-[0.45em] text-muted-foreground uppercase">
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

function MobileOpener({
  photo,
  imageBaseUrl,
  progress,
}: {
  photo: PhotoSummary;
  imageBaseUrl: string;
  progress: MotionValue<number>;
}) {
  const parallax = useParallax(progress, 0);
  const revealClip = useTransform(() => {
    const inset = revealInset(progress.get());
    return `inset(${inset}% 0% ${inset}% 0%)`;
  });
  const revealFilter = useTransform(() => {
    const [brightness, contrast] = revealTone(progress.get());
    return `brightness(${brightness}) contrast(${contrast})`;
  });

  return (
    <div
      className="relative w-full shrink-0 overflow-hidden"
      style={{ height: `calc(100dvh - ${GUTTER_PX * 2}px)` }}
    >
      <motion.div
        style={{ clipPath: revealClip, filter: revealFilter }}
        className="absolute inset-0 overflow-hidden"
      >
        <FrameImage
          photo={photo}
          imageBaseUrl={imageBaseUrl}
          parallax={parallax}
          priority
        />
      </motion.div>
    </div>
  );
}

function MobileFrame({
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
      className="relative w-full shrink-0 overflow-hidden"
      style={{ aspectRatio: String(photoAspect(photo)) }}
    >
      <FrameImage
        photo={photo}
        imageBaseUrl={imageBaseUrl}
        parallax={parallax}
      />
    </div>
  );
}

function FrameImage({
  photo,
  imageBaseUrl,
  parallax,
  priority = false,
}: {
  photo: PhotoSummary;
  imageBaseUrl: string;
  parallax: MotionValue<string>;
  priority?: boolean;
}) {
  return (
    <motion.div
      className="absolute inset-x-0 will-change-transform"
      style={{
        y: parallax,
        top: `${IMAGE_TOP_PCT}%`,
        height: `${IMAGE_HEIGHT_PCT}%`,
      }}
    >
      <PhotoMedia
        photo={photo}
        imageBaseUrl={imageBaseUrl}
        sizes={IMAGE_SIZES}
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
  const magnitude = PARALLAX_PCT[index % PARALLAX_PCT.length] ?? 12;
  // y percentages resolve against the over-scaled image, not the frame.
  const shift = (magnitude * 100) / IMAGE_HEIGHT_PCT;
  return useTransform(progress, TRAVEL, [`-${shift}%`, `${shift}%`]);
}

/** Vertical inset of the opener: half open, a band at the flash, then full. */
function revealInset(progress: number): number {
  if (progress <= REVEAL_FLASH_AT) {
    return 50 - 20 * (progress / REVEAL_FLASH_AT);
  }
  const t = Math.min(
    1,
    (progress - REVEAL_FLASH_AT) / (REVEAL[1] - REVEAL_FLASH_AT),
  );
  return 30 * (1 - t);
}

/** Over-exposed at the flash, settling to the true photograph. */
function revealTone(progress: number): [number, number] {
  if (progress <= REVEAL_FLASH_AT) {
    const t = progress / REVEAL_FLASH_AT;
    return [2.6 - 0.7 * t, 1.06 - 0.03 * t];
  }
  const t = Math.min(
    1,
    (progress - REVEAL_FLASH_AT) / (REVEAL[1] - REVEAL_FLASH_AT),
  );
  return [1.9 - 0.9 * t, 1.03 - 0.03 * t];
}

function MobileNameCard() {
  return (
    <div className="border border-rule bg-bg px-4 py-3">
      <p
        id="intro-name"
        className="text-center font-heading text-[clamp(2.5rem,9vw,8rem)] leading-[0.85] tracking-[-0.045em] pb-[0.18em] text-fg"
      >
        {site.handle}
      </p>
      <p className="mt-2 text-center text-xs text-muted-foreground">
        {site.name}
      </p>
    </div>
  );
}
