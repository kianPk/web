import { useAuthStore } from "~/stores/AuthStore";
import { toast } from "@/components/ui/toast";

let checkedMe = false;
// The verification the guest-shell fast path below starts and does not wait
// for. Held only while it is in the air: once it lands, authStore.me is the
// answer, and a promise kept past that would still be saying "signed in" after
// a sign-out.
let verifyingMe: Promise<boolean> | null = null;

function isGuestShellRoute(path: string): boolean {
  // Guests only see the marketing landing + auth. Everything else (leaderboard,
  // watch, apps…) used to render behind TopNav; bounce them to login so after
  // Steam they land in the LeftNav shell instead.
  if (path === "/" || path === "/login") {
    return true;
  }
  if (path.startsWith("/auth")) {
    return true;
  }
  // Embeds / popouts stay reachable without a session.
  if (path.startsWith("/embed/") || path.startsWith("/match-popout")) {
    return true;
  }
  return false;
}

export default defineNuxtRouteMiddleware(async (to) => {
  if (process.server) return;

  if (to.query.error) {
    const errorMessage = Array.isArray(to.query.error)
      ? to.query.error[0]
      : to.query.error;

    if (typeof errorMessage === "string") {
      toast({
        variant: "destructive",
        title: useNuxtApp().$i18n.t("common.error"),
        description: errorMessage,
      });
    }

    // Remove error from URL to prevent showing toast again on refresh
    const query = { ...to.query };
    delete query.error;
    return navigateTo({
      path: to.path,
      query,
    });
  }

  const authStore = useAuthStore();

  let hasMe: boolean = authStore.me?.steam_id ? true : false;

  // Guest shell + /login always need the real session answer so we don't flash
  // TopNav on a public page, then yank the user after getMe lands.
  const needsRealAnswer =
    !isGuestShellRoute(to.path) ||
    to.path === "/login" ||
    to.path === "/";

  if (!checkedMe) {
    checkedMe = true;

    const verifying = authStore.getMe();
    verifyingMe = verifying;
    void verifying.finally(() => {
      if (verifyingMe === verifying) {
        verifyingMe = null;
      }
    });

    if (needsRealAnswer) {
      hasMe = await verifying;
    }
  } else if (!hasMe && needsRealAnswer && verifyingMe) {
    hasMe = await verifyingMe;
  }

  if (!hasMe && !isGuestShellRoute(to.path)) {
    const redirect =
      to.fullPath === "/"
        ? ""
        : `?redirect=${encodeURIComponent(to.fullPath)}`;
    return navigateTo(`/login${redirect}`);
  }

  if (hasMe && to.path === "/login") {
    if (to.query.redirect) {
      const redirectPath = decodeURIComponent(to.query.redirect as string);
      if (redirectPath.startsWith("/") && !redirectPath.startsWith("//")) {
        return navigateTo(redirectPath);
      }
    }
    return navigateTo("/");
  }
});
