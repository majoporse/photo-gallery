import { useRef } from "react";
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
  const trackRef = useRef<HTMLDivElement>(null);
  const { scrollYProgress } = useScroll({
    target: trackRef,
    offset: ["start start", "end end"],
  });

  const cueOpacity = useTransform(scrollYProgress, [0, 0.02], [1, 0]);
  const nameOpacity = useTransform(
    scrollYProgress,
    [0.42, 0.5, 0.72, 0.8],
    [0, 1, 1, 0],
  );
  const nameY = useTransform(scrollYProgress, [0.42, 0.5], [48, 0]);

  return (
    <section
      aria-labelledby="intro-name"
      ref={trackRef}
      className="relative h-[400vh] bg-fg"
    >
      <div className="sticky top-0 h-dvh overflow-hidden">
        <div aria-hidden="true" className="absolute inset-0">
          {photos.map((photo, index) => (
            <IntroFrame
              key={photo.id}
              photo={photo}
              imageBaseUrl={imageBaseUrl}
              index={index}
              count={photos.length}
              progress={scrollYProgress}
            />
          ))}
          <div className="pointer-events-none absolute inset-0 bg-linear-to-t from-fg/85 via-fg/25 to-fg/10" />
        </div>

        <motion.div
          style={{ opacity: nameOpacity, y: nameY }}
          className="pointer-events-none absolute inset-x-0 bottom-0 px-5 pb-16 sm:px-8 sm:pb-20"
        >
          <p
            id="intro-name"
            className="max-w-4xl font-heading text-[clamp(3rem,10vw,9rem)] leading-[0.9] tracking-[-0.045em] text-balance text-bg"
          >
            {site.name}
          </p>
          {site.tagline ? (
            <p className="mt-4 max-w-xl text-base text-bg/75">
              {site.tagline}
            </p>
          ) : null}
        </motion.div>

        <motion.div
          aria-hidden="true"
          style={{ opacity: cueOpacity }}
          className="pointer-events-none fixed inset-x-0 bottom-6 flex justify-center"
        >
          <span className="text-[0.65rem] tracking-[0.3em] text-bg/80 uppercase">
            Scroll
          </span>
        </motion.div>
      </div>
    </section>
  );
}

function IntroFrame({
  photo,
  imageBaseUrl,
  index,
  count,
  progress,
}: {
  photo: PhotoSummary;
  imageBaseUrl: string;
  index: number;
  count: number;
  progress: MotionValue<number>;
}) {
  const step = 1 / Math.max(count, 1);
  const overlap = step * 0.45;
  const start = index * step;
  const end = Math.min(1, start + step);
  const isFirst = index === 0;
  const isLast = index === count - 1;

  const opacity = useTransform(
    progress,
    ...fadeRanges({ start, end, overlap, isFirst, isLast, count }),
  );
  const scale = useTransform(
    progress,
    [Math.max(0, start - overlap), Math.min(1, end + overlap)],
    [1.08, 1],
  );

  return (
    <motion.div
      data-intro-slide
      className="absolute inset-0 will-change-[opacity,transform]"
      style={{ opacity, scale }}
    >
      <PhotoMedia
        photo={photo}
        imageBaseUrl={imageBaseUrl}
        sizes="100vw"
        loading={isFirst ? "eager" : "lazy"}
        fetchPriority={isFirst ? "high" : "auto"}
        alt=""
        imgClassName="h-full w-full object-cover"
      />
    </motion.div>
  );
}

/**
 * Each slide holds while it is the current one, then overlaps with its
 * neighbour. The first slide starts fully visible at progress 0 and the last
 * one holds through the end of the track.
 */
function fadeRanges({
  start,
  end,
  overlap,
  isFirst,
  isLast,
  count,
}: {
  start: number;
  end: number;
  overlap: number;
  isFirst: boolean;
  isLast: boolean;
  count: number;
}): [number[], number[]] {
  if (count === 1) {
    return [[0, 1], [1, 1]];
  }

  if (isFirst) {
    return [[0, end - overlap, end + overlap], [1, 1, 0]];
  }

  if (isLast) {
    return [[start - overlap, start + overlap], [0, 1]];
  }

  return [
    [start - overlap, start + overlap, end - overlap, end + overlap],
    [0, 1, 1, 0],
  ];
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
        <div className="pointer-events-none absolute inset-0 bg-linear-to-t from-fg/85 via-fg/25 to-fg/10" />
      </div>

      <div className="relative flex h-full flex-col justify-end px-5 pb-40 sm:px-8 sm:pb-44">
        <p
          id="intro-name"
          className="max-w-4xl font-heading text-[clamp(3rem,10vw,9rem)] leading-[0.9] tracking-[-0.045em] text-balance text-bg"
        >
          {site.name}
        </p>
        {site.tagline ? (
          <p className="mt-4 max-w-xl text-base text-bg/75">{site.tagline}</p>
        ) : null}
      </div>
    </section>
  );
}
