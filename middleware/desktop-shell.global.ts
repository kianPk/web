/**
 * YGuard desktop client / ?desktop=1: keep the Faceit-style launcher chrome.
 * Deep links (game-servers, clubs, …) must not swap to the site sidebar layout.
 */
export default defineNuxtRouteMiddleware((to, from) => {
  if (import.meta.server) return;

  const ua = navigator.userAgent || "";
  const flagged =
    to.query.desktop === "1" ||
    to.query.desktop === "true" ||
    from.query.desktop === "1" ||
    from.query.desktop === "true" ||
    document.documentElement.dataset.yguardDesktop === "1" ||
    ua.includes("YGuardClient/");

  if (!flagged) return;

  document.documentElement.dataset.yguardDesktop = "1";

  if (to.query.desktop !== "1" && to.query.desktop !== "true") {
    return navigateTo(
      {
        path: to.path,
        query: { ...to.query, desktop: "1" },
        hash: to.hash,
      },
      { replace: true },
    );
  }

  // Auth handoff pages keep a minimal chrome; everything else stays in launcher.
  if (to.path.startsWith("/auth/")) return;

  setPageLayout("launcher");
});
