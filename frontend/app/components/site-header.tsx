import { NavLink } from "react-router";
import { ThemeToggle } from "~/components/theme-toggle";
import { cn } from "~/lib/utils";

const links = [
  { to: "/", label: "Home", end: true },
  { to: "/collections", label: "Collections", end: false },
  { to: "/about", label: "About", end: false },
  { to: "/admin", label: "Admin", end: false },
];

export function SiteHeader() {
  return (
    <header className="border-b border-rule bg-bg" data-site-header>
      <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-4">
        <NavLink to="/" className="font-heading text-xl tracking-tight">
          Gallery
        </NavLink>

        <nav aria-label="Main">
          <ul className="flex flex-wrap items-center gap-5">
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
