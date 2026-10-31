import { NavLink } from "react-router";
import { ThemeToggle } from "~/components/theme-toggle";
import { site } from "~/content/site";
import { cn } from "~/lib/utils";

const links = [
  { to: "/", label: "Home", end: true },
  { to: "/collections", label: "Collections", end: false },
  { to: "/about", label: "About", end: false },
];

export function SiteHeader() {
  return (
    <header className="border-b border-rule bg-bg" data-site-header>
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-3 sm:gap-4 sm:px-5 sm:py-4">
        <NavLink
          to="/"
          className="font-heading text-lg tracking-tight sm:text-xl"
        >
          {site.wordmark}
        </NavLink>

        <nav aria-label="Main">
          <ul className="flex items-center gap-3 sm:gap-5">
            {links.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  end={link.end}
                  className={({ isActive }) =>
                    cn(
                      "border-b pb-0.5 text-sm tracking-wide",
                      isActive
                        ? "border-accent text-fg"
                        : "border-transparent text-muted-foreground hover:text-fg",
                    )
                  }
                >
                  {link.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <ThemeToggle />
      </div>
    </header>
  );
}
