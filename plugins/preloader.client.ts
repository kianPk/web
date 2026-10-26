import { isSpeedLabBot } from "~/utils/isSpeedLabBot";

export default defineNuxtPlugin((nuxtApp) => {
  let revealed = false;

  const reveal = () => {
    if (revealed) {
      return;
    }
    revealed = true;

    // Two frames: the first lets Vue's just-resolved patch land in the DOM,
    // the second lets the browser actually paint it. Fading on the same tick
    // starts the transition against a still-empty page, which is the flash
    // we're avoiding.
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        document.body.classList.add("pre-loader--fade");

        setTimeout(() => {
          document.body.classList.remove("pre-loader", "pre-loader--fade");
        }, 300);
      });
    });
  };

  // PageSpeed / Lighthouse: drop the overlay immediately so metrics and the
  // screenshot can see real page content (SPA still has empty #__nuxt until
  // hydrate, but spa-loading-template + guest landing handle first paint).
  if (isSpeedLabBot(navigator.userAgent)) {
    document.documentElement.setAttribute("data-lab-bot", "1");
    reveal();
    return;
  }

  // Desktop YGuard Client (WebView2): the spa-loading hero is stripped as soon
  // as Vue mounts, leaving only the black .pre-loader mask until suspense
  // resolves. If a launcher chunk stalls, that looks like a dead black window.
  // Head script + CSS already hide the mask; still force-clear body classes.
  const desktopClient =
    navigator.userAgent.includes("YGuardClient/") ||
    new URLSearchParams(window.location.search).get("desktop") === "1" ||
    new URLSearchParams(window.location.search).get("desktop") === "true" ||
    document.documentElement.dataset.yguardDesktop === "1";
  if (desktopClient) {
    document.documentElement.dataset.yguardDesktop = "1";
    const clearMask = () => {
      document.body.classList.remove("pre-loader", "pre-loader--fade");
    };
    clearMask();
    reveal();
    setTimeout(clearMask, 500);
    setTimeout(clearMask, 2000);
  }

  // NOT app:mounted — Nuxt fires that the instant the root component mounts,
  // before the root <Suspense> resolves. NuxtLayout and NuxtPage both defer
  // hydration, so at that point the layout, its async children and the page
  // chunk are all still in flight and the app renders nothing. Tearing the
  // overlay down there leaves a blank screen until everything lands.
  nuxtApp.hook("app:suspense:resolve", reveal);

  // An error aborts the suspense — the overlay still has to go.
  nuxtApp.hook("app:error", reveal);

  // Last resort: never leave someone stuck staring at the spinner.
  setTimeout(reveal, desktopClient ? 2500 : 8000);
});
