import {
  SITE_URL,
  SEO_KEYWORDS_EN,
  SEO_KEYWORDS_FA,
  buildWebsiteJsonLd,
  seoCopy,
  type SeoPageKey,
} from "~/utils/siteSeo";

/**
 * Apply page SEO (title, description, Open Graph, Twitter, JSON-LD).
 * Persian-first for yguard.ir; English when locale is not fa.
 */
export function useSiteSeo(page: SeoPageKey = "home") {
  const { locale } = useI18n();
  const route = useRoute();
  const { brandName } = useBranding();

  const isFa = computed(() =>
    String(locale.value || "").toLowerCase().startsWith("fa"),
  );

  const copy = computed(() => seoCopy(page, isFa.value));
  const brand = computed(() => brandName.value || "YGuard");
  const canonical = computed(() => {
    const path = route.path === "/" ? "" : route.path;
    return `${SITE_URL}${path}`;
  });

  const title = computed(() => {
    // Home already includes brand; other pages get | YGuard / وای گارد
    if (page === "home") return copy.value.title;
    return `${copy.value.title}`;
  });

  useHead({
    htmlAttrs: {
      lang: () => (isFa.value ? "fa" : "en"),
      dir: () => (isFa.value ? "rtl" : "ltr"),
    },
    title: () => title.value,
    // Full SEO title already includes the brand — don't append again.
    titleTemplate: (t) => t || title.value,
    link: [
      { rel: "canonical", href: () => canonical.value },
      {
        rel: "alternate",
        hreflang: "fa-IR",
        href: () => canonical.value,
      },
      {
        rel: "alternate",
        hreflang: "en",
        href: () => canonical.value,
      },
      {
        rel: "alternate",
        hreflang: "x-default",
        href: () => canonical.value,
      },
    ],
    script: [
      {
        key: "ld-yguard",
        type: "application/ld+json",
        children: () => JSON.stringify(buildWebsiteJsonLd(isFa.value)),
      },
    ],
  });

  useSeoMeta({
    title: () => title.value,
    description: () => copy.value.description,
    keywords: () => (isFa.value ? SEO_KEYWORDS_FA : SEO_KEYWORDS_EN),
    author: () => brand.value,
    robots: "index, follow, max-image-preview:large",
    ogTitle: () => title.value,
    ogDescription: () => copy.value.description,
    ogType: "website",
    ogUrl: () => canonical.value,
    ogSiteName: () => (isFa.value ? "وای گارد | YGuard" : "YGuard"),
    ogLocale: () => (isFa.value ? "fa_IR" : "en_US"),
    ogImage: `${SITE_URL}/favicon/512.png`,
    ogImageAlt: () =>
      isFa.value
        ? "وای گارد — پلتفرم کانتر استرایک ۲"
        : "YGuard — Counter-Strike 2 platform",
    twitterCard: "summary_large_image",
    twitterTitle: () => title.value,
    twitterDescription: () => copy.value.description,
    twitterImage: `${SITE_URL}/favicon/512.png`,
  });

  // Secondary locale tag for bilingual indexing
  useHead({
    meta: [
      {
        property: "og:locale:alternate",
        content: () => (isFa.value ? "en_US" : "fa_IR"),
      },
    ],
  });

  return { copy, isFa, canonical, title };
}
