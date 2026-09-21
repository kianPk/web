export default defineNuxtPlugin((nuxtApp) => {
  let started = false;

  const boot = async () => {
    if (started) return;
    started = true;
    try {
      const [{ OverlayScrollbarsComponent }] = await Promise.all([
        import("overlayscrollbars-vue"),
        import("overlayscrollbars/overlayscrollbars.css"),
      ]);
      nuxtApp.vueApp.component(
        "OverlayScrollbars",
        OverlayScrollbarsComponent,
      );
    } catch (error) {
      console.warn("OverlayScrollbars deferred load failed", error);
    }
  };

  const idleBoot = () => {
    if ("requestIdleCallback" in window) {
      window.requestIdleCallback(() => void boot(), { timeout: 8000 });
      return;
    }
    setTimeout(() => void boot(), 3000);
  };

  const isGuestLanding = () => {
    const path = window.location.pathname || "/";
    return path === "/" || path === "";
  };

  const schedule = () => {
    if (typeof window === "undefined") return;
    // Guest marketing page never mounts OverlayScrollbars — skip until leave.
    if (isGuestLanding()) {
      const stop = nuxtApp.hook("page:finish", () => {
        if (!isGuestLanding()) {
          stop();
          idleBoot();
        }
      });
      setTimeout(idleBoot, 15000);
      return;
    }
    idleBoot();
  };

  // Wait until the preloader is gone so we don't compete with entry JS / first paint.
  if (document.body.classList.contains("pre-loader")) {
    const observer = new MutationObserver(() => {
      if (!document.body.classList.contains("pre-loader")) {
        observer.disconnect();
        schedule();
      }
    });
    observer.observe(document.body, {
      attributes: true,
      attributeFilter: ["class"],
    });
    setTimeout(schedule, 9000);
    return;
  }

  schedule();
});
