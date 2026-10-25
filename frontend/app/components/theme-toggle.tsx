import { useSyncExternalStore } from "react";
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
      <Button variant="outline" size="sm" disabled aria-label="Theme">
        Theme
      </Button>
    );
  }

  const upcoming = nextTheme(theme);

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
    >
      {LABELS[theme]}
    </Button>
  );
}
