export default defineNuxtPlugin((nuxtApp) => {
  if (!import.meta.client) return;

  nuxtApp.hook("app:mounted", () => {
    const route = useRoute();
    if (route.query.embed !== "1" && route.query.embed !== "true") return;

    document.addEventListener(
      "click",
      (event) => {
        const target = event.target as HTMLElement | null;
        const anchor = target?.closest?.("a[href]") as HTMLAnchorElement | null;
        if (!anchor) return;
        const href = anchor.getAttribute("href");
        if (!href || href.startsWith("#") || href.startsWith("mailto:")) return;
        if (href.startsWith("http") && !href.includes(window.location.host)) {
          return;
        }
        if (href.startsWith("steam:") || href.startsWith("ts3server:")) return;

        try {
          const url = new URL(href, window.location.origin);
          if (url.origin !== window.location.origin) return;
          if (url.searchParams.get("embed") === "1") return;
          url.searchParams.set("embed", "1");
          event.preventDefault();
          void navigateTo(`${url.pathname}${url.search}${url.hash}`);
        } catch {
          // ignore bad hrefs
        }
      },
      true,
    );
  });
});
