import type { ReactNode } from "react";
import { cn } from "~/lib/utils";

/**
 * CSS-columns masonry. Columns lay content out top-to-bottom, so DOM order is
 * column-major: tab and screen-reader order across a row does not match the
 * visual left-to-right order. Accepted for these image grids because each tile
 * is independent and links to its own photo or collection page.
 */
export function Masonry({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <ul className={cn("columns-1 gap-5 sm:columns-2 lg:columns-3", className)}>
      {children}
    </ul>
  );
}

export function MasonryItem({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <li className={cn("mb-5 break-inside-avoid", className)}>{children}</li>
  );
}
