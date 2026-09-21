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

  const schedule = () => {
    if (typeof window === "undefined") return;
    if ("requestIdleCallback" in window) {
      window.requestIdleCallback(() => void boot(), { timeout: 5000 });
      return;
    }
    setTimeout(() => void boot(), 2000);
  };

  // Guest landing never needs this; wait until the preloader is gone so we
  // don't compete with entry JS / first paint (helps mobile TBT).
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
