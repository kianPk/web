/** Site-wide SEO copy for YGuard (Persian-first, English alternate). */

export const SITE_URL = "https://yguard.ir";

export const SEO_KEYWORDS_FA = [
  "وای گارد",
  "وایگارد",
  "YGuard",
  "ی گارد",
  "سی اس",
  "سی اس ۲",
  "سی اس گو",
  "کانتر",
  "کانتر استرایک",
  "کانتر استرایک ۲",
  "CS2",
  "CS:GO",
  "Counter-Strike",
  "مچمیکینگ",
  "مچ میکینگ",
  "رنک سی اس",
  "رنک CS2",
  "سرور سی اس",
  "سرور CS2",
  "سرور پابلیک سی اس",
  "فیستک",
  "انتی چیت",
  "آنتی چیت",
  "آنتی چیت سی اس",
  "تورنمنت سی اس",
  "لیگ سی اس",
  "ایلو",
  "Elo CS2",
  "اسکرم",
  "فاستیک",
  "پلتفرم سی اس ایران",
].join("، ");

export const SEO_KEYWORDS_EN = [
  "YGuard",
  "CS2",
  "Counter-Strike 2",
  "CSGO",
  "CS2 matchmaking Iran",
  "CS2 ranked",
  "CS2 anticheat",
  "CS2 public servers",
  "CS2 tournament",
  "Faceit alternative Iran",
].join(", ");

export type SeoPageKey = "home" | "cs2" | "anticheat" | "servers";

export type SeoPageCopy = {
  title: string;
  description: string;
  h1: string;
};

export const SEO_PAGES_FA: Record<SeoPageKey, SeoPageCopy> = {
  home: {
    title: "وای گارد | مچ‌میکینگ و رنک CS2 | پلتفرم کانتر استرایک ایران",
    description:
      "وای گارد (YGuard) پلتفرم رقابتی کانتر استرایک ۲ در ایران: مچ‌میکینگ رنک، سرور پابلیک CS2، تورنمنت، اسکرم، آنتی‌چیت اختصاصی و سیستم Elo. همین حالا بازی کن.",
    h1: "وای گارد — مچ‌میکینگ و رنک کانتر استرایک ۲",
  },
  cs2: {
    title: "مچ‌میکینگ CS2 و رنک سی اس | وای گارد",
    description:
      "مچ‌میکینگ کانتر استرایک ۲ با Elo واقعی، صف Competitive و Wingman، سرورهای اختصاصی ایران. رنک بگیر، تیم بساز، در وای گارد رقابت کن.",
    h1: "مچ‌میکینگ و رنک CS2 در وای گارد",
  },
  anticheat: {
    title: "آنتی‌چیت CS2 وای گارد | YGuard Anti-Cheat",
    description:
      "آنتی‌چیت اختصاصی وای گارد برای کانتر استرایک ۲: Secure Boot، TPM، HVCI و لانچر ویندوز. بازی تمیز در رنک، پابلیک و کاستوم.",
    h1: "آنتی‌چیت کانتر استرایک ۲ — YGuard AC",
  },
  servers: {
    title: "سرور پابلیک و Dedicated CS2 | وای گارد",
    description:
      "سرورهای پابلیک و اختصاصی کانتر استرایک ۲ روی وای گارد: پینگ پایین، مچ رسمی، کاستوم لابی و مدیریت کامل سرورهای CS2.",
    h1: "سرورهای CS2 پابلیک و اختصاصی",
  },
};

export const SEO_PAGES_EN: Record<SeoPageKey, SeoPageCopy> = {
  home: {
    title: "YGuard | CS2 Matchmaking & Ranked | Counter-Strike Platform",
    description:
      "YGuard is a competitive Counter-Strike 2 platform: ranked matchmaking, public CS2 servers, tournaments, scrims, Elo, and a dedicated anti-cheat. Play now.",
    h1: "YGuard — CS2 matchmaking and ranked play",
  },
  cs2: {
    title: "CS2 Matchmaking & Ranked | YGuard",
    description:
      "Ranked Counter-Strike 2 matchmaking with real Elo, Competitive and Wingman queues, and dedicated servers. Climb ranks on YGuard.",
    h1: "CS2 matchmaking and ranked on YGuard",
  },
  anticheat: {
    title: "YGuard Anti-Cheat for CS2",
    description:
      "YGuard Anti-Cheat for Counter-Strike 2: Secure Boot, TPM, HVCI, and a Windows launcher. Clean play on ranked, public, and custom servers.",
    h1: "CS2 Anti-Cheat — YGuard AC",
  },
  servers: {
    title: "CS2 Public & Dedicated Servers | YGuard",
    description:
      "Public and dedicated Counter-Strike 2 servers on YGuard: low latency, official matches, custom lobbies, and full CS2 server management.",
    h1: "CS2 public and dedicated servers",
  },
};

export function seoCopy(page: SeoPageKey, fa: boolean): SeoPageCopy {
  return fa ? SEO_PAGES_FA[page] : SEO_PAGES_EN[page];
}

export function buildWebsiteJsonLd(fa: boolean) {
  const home = seoCopy("home", fa);
  return {
    "@context": "https://schema.org",
    "@graph": [
      {
        "@type": "Organization",
        "@id": `${SITE_URL}/#organization`,
        name: "YGuard",
        alternateName: ["وای گارد", "وایگارد", "Y Guard"],
        url: SITE_URL,
        logo: `${SITE_URL}/favicon/512.png`,
        description: home.description,
        sameAs: [],
      },
      {
        "@type": "WebSite",
        "@id": `${SITE_URL}/#website`,
        url: SITE_URL,
        name: "YGuard",
        alternateName: "وای گارد",
        description: home.description,
        inLanguage: ["fa-IR", "en"],
        publisher: { "@id": `${SITE_URL}/#organization` },
        potentialAction: {
          "@type": "SearchAction",
          target: `${SITE_URL}/leaderboard?q={search_term_string}`,
          "query-input": "required name=search_term_string",
        },
      },
      {
        "@type": "SoftwareApplication",
        name: "YGuard Anti-Cheat",
        alternateName: "آنتی چیت وای گارد",
        applicationCategory: "GameApplication",
        operatingSystem: "Windows",
        url: `${SITE_URL}/anticheat`,
        offers: { "@type": "Offer", price: "0", priceCurrency: "IRR" },
        description: seoCopy("anticheat", fa).description,
      },
    ],
  };
}
