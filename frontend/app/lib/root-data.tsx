import { createContext, useContext, type ReactNode } from "react";
import type { Theme } from "./theme";

export interface RootData {
  theme: Theme;
  fixtures: boolean;
  publicApiUrl: string;
  publicEventsUrl: string;
  imageBaseUrl: string;
  siteUrl: string;
}

const RootDataContext = createContext<RootData | null>(null);

export function RootDataProvider({
  value,
  children,
}: {
  value: RootData;
  children: ReactNode;
}) {
  return (
    <RootDataContext.Provider value={value}>
      {children}
    </RootDataContext.Provider>
  );
}

export function useRootData(): RootData {
  const value = useContext(RootDataContext);
  if (!value) {
    throw new Error("useRootData must be used inside RootDataProvider");
  }

  return value;
}
