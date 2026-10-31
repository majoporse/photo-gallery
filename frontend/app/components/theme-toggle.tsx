import { useSyncExternalStore } from "react";
import { Contrast, Moon, Sun } from "lucide-react";
import { Button } from "~/components/ui/button";
import {
  parseTheme,
  THEMES,
  writeThemeCookie,
  type Theme,
} from "~/lib/theme";

const LABELS: Record<Theme, string> = {
  paper: "Paper",
  ink: "Ink",
  grey: "Grey",
};

const ICONS: Record<Theme, typeof Sun> = {
  paper: Sun,
  ink: Moon,
  grey: Contrast,
};

const listeners = new Set<() => void>();

function subscribe(listener: () => void) {
  listeners.add(listener);
  window.addEventListener("storage", listener);
  return () => {
    listeners.delete(listener);
    window.removeEventListener("storage", listener);
  };
}

function getSnapshot(): Theme | null {
  if (typeof document === "undefined") {
    return null;
  }

  return parseTheme(document.documentElement.dataset.theme);
}

function getServerSnapshot(): Theme | null {
  return null;
}

function nextTheme(theme: Theme): Theme {
  const index = THEMES.indexOf(theme);
  return THEMES[(index + 1) % THEMES.length] ?? "paper";
}

export function ThemeToggle() {
  const theme = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  if (!theme) {
    return (
      <Button
        variant="outline"
        size="sm"
        disabled
        aria-label="Theme"
        className="px-2 sm:px-2.5"
      >
        <Sun aria-hidden="true" className="sm:hidden" />
        <span className="hidden sm:inline">Theme</span>
      </Button>
    );
  }

  const upcoming = nextTheme(theme);
  const Icon = ICONS[theme];

  return (
    <Button
      variant="outline"
      size="sm"
      onClick={() => {
        document.documentElement.dataset.theme = upcoming;
        writeThemeCookie(upcoming);
        for (const listener of listeners) {
          listener();
        }
      }}
      aria-label={`Theme: ${LABELS[theme]}. Switch to ${LABELS[upcoming]}.`}
      className="px-2 sm:px-2.5"
    >
      <Icon aria-hidden="true" className="sm:hidden" />
      <span className="hidden sm:inline">{LABELS[theme]}</span>
    </Button>
  );
}
