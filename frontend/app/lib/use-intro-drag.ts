import {
  useRef,
  type PointerEvent as ReactPointerEvent,
  type RefObject,
} from "react";

interface DragState {
  pointerId: number;
  x: number;
  y: number;
  top: number;
  bottom: number;
}

export interface IntroDragHandlers {
  onPointerDown: (event: ReactPointerEvent<HTMLDivElement>) => void;
  onPointerMove: (event: ReactPointerEvent<HTMLDivElement>) => void;
  onPointerUp: (event: ReactPointerEvent<HTMLDivElement>) => void;
  onPointerCancel: (event: ReactPointerEvent<HTMLDivElement>) => void;
  onLostPointerCapture: () => void;
}

/**
 * Mouse and pen drag-to-scroll for the intro. Touch keeps native scrolling.
 *
 * Drags write straight to the window scroll position; the intro's spring
 * smooths the visuals, so releasing settles instead of stopping dead. Drags
 * stay inside the intro, the same way a scrollbar drag would.
 */
export function useIntroDrag(
  sectionRef: RefObject<HTMLElement | null>,
): IntroDragHandlers {
  const dragRef = useRef<DragState | null>(null);

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.pointerType === "touch" || event.button !== 0) {
      return;
    }

    const section = sectionRef.current;
    if (!section) {
      return;
    }

    const { top, bottom } = scrollRange(section);
    if (window.scrollY < top || window.scrollY > bottom) {
      return;
    }

    dragRef.current = {
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      top,
      bottom,
    };
    event.currentTarget.setPointerCapture(event.pointerId);
    event.preventDefault();
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    const dx = event.clientX - drag.x;
    const dy = event.clientY - drag.y;
    if (dx === 0 && dy === 0) {
      return;
    }

    drag.x = event.clientX;
    drag.y = event.clientY;

    // Content follows the pointer: pulling left or up advances the sequence.
    const distance = Math.abs(dx) >= Math.abs(dy) ? -dx : -dy;
    const next = Math.min(
      drag.bottom,
      Math.max(drag.top, window.scrollY + distance),
    );
    window.scrollTo(0, next);
    event.preventDefault();
  };

  const endDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }

    dragRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  };

  return {
    onPointerDown,
    onPointerMove,
    onPointerUp: endDrag,
    onPointerCancel: endDrag,
    onLostPointerCapture: () => {
      dragRef.current = null;
    },
  };
}

function scrollRange(section: HTMLElement): { top: number; bottom: number } {
  const top = section.getBoundingClientRect().top + window.scrollY;
  return {
    top,
    bottom: Math.max(top, top + section.offsetHeight - window.innerHeight),
  };
}
