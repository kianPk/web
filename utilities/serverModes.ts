import cleanMapName from "~/utilities/cleanMapName";

export type ServerModeKey = "duels" | "2x2" | "awp";

export type ServerModeCommand = {
  command: string;
  /** i18n key under pages.servers.commands */
  descriptionKey: string;
};

export type ServerModeDefinition = {
  key: ServerModeKey;
  cover: string;
  accent: string;
  order: number;
  /** Slots a server of this mode usually runs with, used when one has none set. */
  slots: number;
  commands: ServerModeCommand[];
  /** The rotation MapChooser cycles through, as players know the maps. */
  maps: string[];
  /** Number of FAQ entries under pages.servers.modes.<key>.faq */
  faq: number;
};

const SCREENSHOT = (map: string) => `/img/maps/screenshots/${map}.webp`;

const MAP_VOTE_COMMANDS: ServerModeCommand[] = [
  { command: "!rtv", descriptionKey: "rtv" },
  { command: "!nominate", descriptionKey: "nominate" },
  { command: "!timeleft", descriptionKey: "timeleft" },
  { command: "!nextmap", descriptionKey: "nextmap" },
];

export const SERVER_MODES: ServerModeDefinition[] = [
  {
    key: "duels",
    cover: SCREENSHOT("de_mirage"),
    accent: "#f5a524",
    order: 10,
    slots: 18,
    commands: [
      { command: "!guns", descriptionKey: "guns" },
      { command: "!rounds", descriptionKey: "rounds" },
      { command: "!queue", descriptionKey: "queue" },
      { command: "!afk", descriptionKey: "afk" },
      ...MAP_VOTE_COMMANDS,
    ],
    maps: ["Mirage Duels", "Redline", "Anubis Duels", "Forgotten Yard"],
    faq: 3,
  },
  {
    key: "2x2",
    cover: SCREENSHOT("de_inferno"),
    accent: "#3e8ed0",
    order: 20,
    slots: 4,
    commands: MAP_VOTE_COMMANDS,
    maps: ["Inferno", "Nuke", "Overpass", "Vertigo", "Poseidon"],
    faq: 2,
  },
  {
    key: "awp",
    cover: SCREENSHOT("de_vertigo"),
    accent: "#46a758",
    order: 30,
    slots: 20,
    commands: MAP_VOTE_COMMANDS,
    maps: ["awp_lego_2", "awp_creek", "awp_roost_fp", "awp_gony_v2"],
    faq: 3,
  },
];

export function serverModeDefinition(
  key: string,
): ServerModeDefinition | undefined {
  return SERVER_MODES.find((mode) => mode.key === key);
}

type ServerLike = {
  type?: string | null;
  game_mode?: {
    slug?: string | null;
    name?: string | null;
  } | null;
};

/**
 * Which mode a public server lists under, or null when it is none of them.
 * The game mode wins over the Valve type: an AWP box runs as Casual, and a
 * Wingman-type server with no mode is still a 2x2 server.
 */
export function serverModeKey(server: ServerLike): ServerModeKey | null {
  const slug = (server.game_mode?.slug || "").toLowerCase();
  if (slug === "duels" || slug === "2x2" || slug === "awp") return slug;

  const text = `${slug} ${server.game_mode?.name ?? ""}`.toLowerCase();
  if (text.trim()) {
    if (/awp/.test(text)) return "awp";
    if (/duel|arena|1v1/.test(text)) return "duels";
    if (/wingman|2v2|2x2/.test(text)) return "2x2";
    return null;
  }

  return server.type === "Wingman" ? "2x2" : null;
}

/** Workshop maps report as workshop/<id>/<name>; the card wants <name>. */
export function liveMapName(raw: string | null | undefined): string {
  const value = String(raw || "").trim();
  if (!value || value === "unknown") return "default";
  return value.replace(/^workshop\//, "").split("/").pop() || "default";
}

const MAP_LABELS: Record<string, string> = {
  am_mirage_middle: "Mirage Duels",
  am_anubis_p: "Anubis Duels",
  am_redline: "Redline",
};

/** The name a player knows a map by: "Mirage Duels", "Inferno", awp_lego_2. */
export function mapLabel(map: string): string {
  if (!map || map === "default") return "—";
  if (MAP_LABELS[map]) return MAP_LABELS[map];
  if (/^(de|cs|ar)_/.test(map)) return cleanMapName(map);
  return map;
}
