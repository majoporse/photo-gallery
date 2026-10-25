import { useState } from "react";
import { useSearchParams } from "react-router";
import { useMutation } from "@tanstack/react-query";
import { adminLogin, AdminApiError } from "~/api/admin";
import { Button } from "~/components/ui/button";

const DEFAULT_RETURN_TO = "/admin/collections";

function safeReturnTo(value: string | null): string {
  if (!value) {
    return DEFAULT_RETURN_TO;
  }

  // Only admin screens are allowed, so a crafted link cannot bounce elsewhere.
  return /^\/admin\/(?:collections|photos)(?:[/?]|$)/.test(value)
    ? value
    : DEFAULT_RETURN_TO;
}

function loginErrorMessage(error: unknown): string {
  if (error instanceof AdminApiError) {
    if (error.status === 401) {
      return "Wrong username or password.";
    }
    if (error.status === 429) {
      return "Too many sign-in attempts. The limit is five per minute — wait a minute and try again.";
    }
    return error.message;
  }

  return error instanceof Error ? error.message : "Sign-in failed.";
}

export default function AdminLogin() {
  const [searchParams] = useSearchParams();
  const returnTo = safeReturnTo(searchParams.get("returnTo"));

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const login = useMutation({
    mutationFn: (credentials: { username: string; password: string }) =>
      adminLogin(credentials.username, credentials.password),
    onSuccess: () => {
      window.location.assign(returnTo);
    },
  });

  const error = login.error ? loginErrorMessage(login.error) : null;

  return (
    <div className="mx-auto max-w-md">
      <p className="text-xs tracking-[0.2em] text-muted-foreground uppercase">
        Owner
      </p>
      <h1 className="mt-3 font-heading text-4xl tracking-tight">Sign in</h1>
      <p className="mt-4 text-sm text-muted-foreground">
        This is the only account. The session lasts twelve hours and is stored
        in an HttpOnly cookie.
      </p>

      <form
        className="mt-10 space-y-5"
        onSubmit={(event) => {
          event.preventDefault();
          login.mutate({ username, password });
        }}
      >
        <div>
          <label htmlFor="username" className="text-sm">
            Username
          </label>
          <input
            id="username"
            name="username"
            autoComplete="username"
            autoFocus
            required
            value={username}
            onChange={(event) => {
              setUsername(event.target.value);
            }}
            className="mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm outline-none focus-visible:border-accent"
          />
        </div>

        <div>
          <label htmlFor="password" className="text-sm">
            Password
          </label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
            }}
            className="mt-1 block w-full border border-rule bg-bg px-3 py-2 text-sm outline-none focus-visible:border-accent"
          />
        </div>

        <Button
          type="submit"
          disabled={login.isPending}
          className="w-full border-accent bg-accent text-accent-fg hover:bg-accent/85"
        >
          {login.isPending ? "Signing in…" : "Sign in"}
        </Button>

        <p
          className={`min-h-5 text-sm ${error ? "text-destructive" : "text-muted-foreground"}`}
          role={error ? "alert" : "status"}
          aria-live="polite"
        >
          {error ??
            (searchParams.has("returnTo")
              ? "Sign in to continue to the screen you asked for."
              : "")}
        </p>
      </form>
    </div>
  );
}
