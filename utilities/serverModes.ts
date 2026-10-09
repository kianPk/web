export type ServerModeCommand = {
  command: string;
  descriptionKey: string;
};

export type ServerModeDefinition = {
  key: string;
  /** i18n key under pages.servers.modes.<key>, unset for admin-defined modes. */
  i18nKey?: string;
  name?: string;
  description?: string | null;
  cover: string;
  accent: string;
  order: number;
  commands: ServerModeCommand[];
};

const SCREENSHOT = (map: string) => `/img/maps/screenshots/${map}.webp`;

const BUILT_IN: Record<string, Omit<ServerModeDefinition, "key">> = {
  dm: {
    i18nKey: "dm",
    cover: SCREENSHOT("de_mirage"),
    accent: "#e5484d",
    order: 10,
    commands: [
      { command: "!guns", descriptionKey: "guns" },
    ],
  },
  duels: {
    i18nKey: "duels",
    cover: SCREENSHOT("de_dust2"),
    accent: "#f5a524",
    order: 20,
    commands: [
      { command: "!guns", descriptionKey: "guns_preferences" },
      { command: "!rounds", descriptionKey: "rounds" },
      { command: "!afk", descriptionKey: "afk" },
    ],
  },
  wingman: {
    i18nKey: "wingman",
    cover: SCREENSHOT("de_overpass"),
    accent: "#3e8ed0",
    order: 30,
    commands: [],
  },
  awp: {
    i18nKey: "awp",
    cover: SCREENSHOT("de_vertigo"),
    accent: "#46a758",
    order: 40,
    commands: [],
  },
  retake: {
    i18nKey: "retake",
    cover: SCREENSHOT("de_inferno"),
    accent: "#8e4ec6",
    order: 50,
    commands: [],
  },
  competitive: {
    i18nKey: "competitive",
    cover: SCREENSHOT("de_ancient"),
    accent: "#d6409f",
    order: 60,
    commands: [],
  },
  public: {
    i18nKey: "public",
    cover: SCREENSHOT("de_train"),
    accent: "#12a594",
    order: 70,
    commands: [],
  },
  armsrace: {
    i18nKey: "armsrace",
    cover: SCREENSHOT("de_nuke"),
    accent: "#e54d2e",
    order: 80,
    commands: [],
  },
  surf: {
    i18nKey: "surf",
    cover: SCREENSHOT("de_anubis"),
    accent: "#0090ff",
    order: 90,
    commands: [],
  },
  bhop: {
    i18nKey: "bhop",
    cover: SCREENSHOT("de_cache"),
    accent: "#ffb224",
    order: 100,
    commands: [],
  },
  kz: {
    i18nKey: "kz",
    cover: SCREENSHOT("de_thera"),
    accent: "#30a46c",
    order: 110,
    commands: [],
  },
};

type ServerLike = {
  type?: string | null;
  game_mode?: {
    slug?: string | null;
    name?: string | null;
    description?: string | null;
  } | null;
};

/**
 * Which mode tile a server belongs to. A server's game mode wins over its
 * Valve type: an AWP box is usually a Deathmatch-type server running a
 * weapon-restricted mode, and should list under AWP, not Deathmatch.
 */
export function serverModeKey(server: ServerLike): string {
  const mode = server.game_mode;
  const text = `${mode?.slug ?? ""} ${mode?.name ?? ""}`.toLowerCase();

  if (text.trim()) {
    if (/awp/.test(text)) return "awp";
    if (/duel|arena|1v1|aim[_-]?map/.test(text)) return "duels";
    if (/retake/.test(text)) return "retake";
    if (/surf/.test(text)) return "surf";
    if (/bhop|bunny/.test(text)) return "bhop";
    if (/\bkz\b|climb/.test(text)) return "kz";
    if (/wingman|2v2|2x2/.test(text)) return "wingman";
    if (/deathmatch|\bdm\b|\bffa\b/.test(text)) return "dm";
  }

  switch (server.type) {
    case "Deathmatch":
      return "dm";
    case "Wingman":
      return "wingman";
    case "Retake":
      return "retake";
    case "ArmsRace":
      return "armsrace";
    case "Competitive":
      return "competitive";
  }

  if (mode?.slug) {
    return `custom-${mode.slug}`;
  }

  return "public";
}

export function serverModeDefinition(
  key: string,
  sample?: ServerLike,
): ServerModeDefinition {
  const builtIn = BUILT_IN[key];
  if (builtIn) {
    return { key, ...builtIn };
  }

  return {
    key,
    name: sample?.game_mode?.name || key.replace(/^custom-/, ""),
    description: sample?.game_mode?.description ?? null,
    cover: SCREENSHOT("de_mirage"),
    accent: "#f5a524",
    order: 500,
    commands: [],
  };
}

/** Workshop maps report as workshop/<id>/<name>; the card wants <name>. */
export function liveMapName(raw: string | null | undefined): string {
  const value = String(raw || "").trim();
  if (!value || value === "unknown") return "default";
  return value.replace(/^workshop\//, "").split("/").pop() || "default";
}
