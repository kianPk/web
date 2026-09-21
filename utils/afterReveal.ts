/** Run after the SPA pre-loader overlay is gone, then on idle. */
export function afterReveal(run: () => void) {
  if (typeof window === "undefined") {
    return;
  }

  let scheduled = false;

  const idle = () => {
    if (scheduled) return;
    scheduled = true;

    if ("requestIdleCallback" in window) {
      window.requestIdleCallback(run, { timeout: 4000 });
      return;
    }
    setTimeout(run, 800);
  };

  if (!document.body.classList.contains("pre-loader")) {
    idle();
    return;
  }

  const observer = new MutationObserver(() => {
    if (!document.body.classList.contains("pre-loader")) {
      observer.disconnect();
      idle();
    }
  });

  observer.observe(document.body, {
    attributes: true,
    attributeFilter: ["class"],
  });

  setTimeout(() => {
    observer.disconnect();
    idle();
  }, 10000);
}
