import { useAuthStore } from "~/stores/AuthStore";
import { toast } from "@/components/ui/toast";

let checkedMe = false;
// The verification the guest-shell fast path below starts and does not wait
// for. Held only while it is in the air: once it lands, authStore.me is the
// answer, and a promise kept past that would still be saying "signed in" after
// a sign-out.
let verifyingMe: Promise<boolean> | null = null;

function isGuestShellRoute(path: string): boolean {
  // Guests only see the marketing landing + auth + public SEO pages.
  if (
    path === "/" ||
    path === "/login" ||
    path === "/cs2" ||
    path === "/anticheat" ||
    path === "/servers"
  ) {
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

function isColdGuestLanding(path: string): boolean {
  return path === "/" || path === "";
}

function startGetMe(authStore: ReturnType<typeof useAuthStore>) {
  const verifying = authStore.getMe();
  verifyingMe = verifying;
  void verifying.finally(() => {
    if (verifyingMe === verifying) {
      verifyingMe = null;
    }
  });
  return verifying;
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

  // Protected routes + /login need the real session before continuing.
  // Guest landing (`/`) paints first — index.vue starts getMe after reveal.
  const needsRealAnswer =
    !isGuestShellRoute(to.path) || to.path === "/login";

  if (!checkedMe) {
    checkedMe = true;

    if (isColdGuestLanding(to.path)) {
      // Skip: pages/index.vue kicks getMe after the preloader reveal so TBT
      // isn't inflated by GraphQL + auth store fan-out during boot.
    } else {
      const verifying = startGetMe(authStore);
      if (needsRealAnswer) {
        hasMe = await verifying;
      }
    }
  } else if (!hasMe && needsRealAnswer) {
    if (!authStore.hasCheckedSession && !verifyingMe) {
      startGetMe(authStore);
    }
    if (verifyingMe) {
      hasMe = await verifyingMe;
    } else {
      hasMe = !!authStore.me?.steam_id;
    }
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
