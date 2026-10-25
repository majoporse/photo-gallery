import { useEffect, useState } from "react";
import useEmblaCarousel from "embla-carousel-react";
import type { PhotoSummary } from "~/api/generated/model";
import { PhotoCard } from "~/components/photo-card";
import { Button } from "~/components/ui/button";
import { CAROUSEL_SIZES } from "~/lib/images";

interface FeaturedCarouselProps {
  photos: readonly PhotoSummary[];
}

const OPTIONS = { align: "start", loop: false, containScroll: "trimSnaps" } as const;

export function FeaturedCarousel({ photos }: FeaturedCarouselProps) {
  const [viewportRef, emblaApi] = useEmblaCarousel(OPTIONS);
  const [canGoBack, setCanGoBack] = useState(false);
  const [canGoForward, setCanGoForward] = useState(false);

  useEffect(() => {
    if (!emblaApi) {
      return;
    }

    const update = () => {
      setCanGoBack(emblaApi.canScrollPrev());
      setCanGoForward(emblaApi.canScrollNext());
    };

    update();
    emblaApi.on("select", update).on("reInit", update);

    return () => {
      emblaApi.off("select", update).off("reInit", update);
    };
  }, [emblaApi]);

  return (
    <section
      aria-label="Featured photographs"
      aria-roledescription="carousel"
      className="mt-6"
      onKeyDown={(event) => {
        if (!emblaApi) {
          return;
        }

        if (event.key === "ArrowLeft") {
          event.preventDefault();
          emblaApi.scrollPrev();
        } else if (event.key === "ArrowRight") {
          event.preventDefault();
          emblaApi.scrollNext();
        }
      }}
    >
      <div className="overflow-hidden" ref={viewportRef}>
        <ul className="-ml-5 flex touch-pan-y">
          {photos.map((photo, index) => (
            <li
              key={photo.id}
              aria-label={`${index + 1} of ${photos.length}`}
              aria-roledescription="slide"
              className="min-w-0 flex-[0_0_100%] pl-5 sm:flex-[0_0_72%] lg:flex-[0_0_58%]"
            >
              <PhotoCard
                photo={photo}
                index={index}
                sizes={CAROUSEL_SIZES}
                priority={index === 0}
              />
            </li>
          ))}
        </ul>
      </div>

      <div className="mt-4 flex items-center justify-between gap-4 border-t border-rule pt-3">
        <p className="text-xs text-muted-foreground">
          {photos.length} featured{" "}
          {photos.length === 1 ? "photograph" : "photographs"}
        </p>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-label="Previous featured photograph"
            disabled={!canGoBack}
            onClick={() => emblaApi?.scrollPrev()}
          >
            Previous
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-label="Next featured photograph"
            disabled={!canGoForward}
            onClick={() => emblaApi?.scrollNext()}
          >
            Next
          </Button>
        </div>
      </div>
    </section>
  );
}
