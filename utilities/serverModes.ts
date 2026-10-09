import cleanMapName from "~/utilities/cleanMapName";

export type ServerModeKey = "duels" | "dm" | "bhop" | "2x2";

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
  /** The rotation until the api's pool (kept in the settings) arrives. */
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
    maps: ["am_map", "Redline NGNW", "Redline"],
    faq: 3,
  },
  {
    key: "dm",
    cover: SCREENSHOT("de_dust2"),
    accent: "#e5484d",
    order: 20,
    slots: 18,
    commands: [
      { command: "!guns", descriptionKey: "dm_guns" },
      { command: "!ak", descriptionKey: "ak" },
      { command: "!m4", descriptionKey: "m4" },
      { command: "!fast", descriptionKey: "respawn_fast" },
      { command: "!medium", descriptionKey: "respawn_medium" },
      { command: "!slow", descriptionKey: "respawn_slow" },
      { command: "!hs", descriptionKey: "hs" },
      { command: "!sounds", descriptionKey: "sounds" },
      { command: "!rtv", descriptionKey: "rtv" },
      { command: "!nominate", descriptionKey: "nominate" },
      { command: "!timeleft", descriptionKey: "timeleft" },
    ],
    maps: [
      "Dust 2",
      "Mirage",
      "Anubis",
      "Cache",
      "Ancient Night",
      "Overpass",
      "Ancient",
      "Train",
      "Inferno",
      "Vertigo",
      "Nuke",
    ],
    faq: 3,
  },
  {
    key: "bhop",
    cover: SCREENSHOT("de_ancient_night"),
    accent: "#46a758",
    order: 30,
    slots: 20,
    commands: [
      { command: "!r", descriptionKey: "restart" },
      { command: "!rtv", descriptionKey: "rtv" },
      { command: "!timeleft", descriptionKey: "timeleft" },
      { command: "!nominate", descriptionKey: "nominate" },
      { command: "!usp", descriptionKey: "weapon" },
      { command: "!hud_s", descriptionKey: "hud" },
      { command: "!fov 110", descriptionKey: "fov" },
      { command: "!spec", descriptionKey: "spec" },
      { command: "!b 1", descriptionKey: "bonus" },
      { command: "!info", descriptionKey: "info" },
      { command: "!wr", descriptionKey: "wr" },
      { command: "!bwr 1", descriptionKey: "bwr" },
      { command: "!pb", descriptionKey: "pb" },
      { command: "!top", descriptionKey: "top" },
      { command: "!practice", descriptionKey: "practice" },
      { command: "!noclip", descriptionKey: "noclip" },
      { command: "!resume", descriptionKey: "resume" },
    ],
    maps: [
      "bhop_emevaelx3",
      "bhop_dust_temple",
      "bhop_rc_nuclear",
      "bhop_colour",
      "bhop_cherryblossom",
      "bhop_bug100_2nd",
      "bhop_skylook2",
      "bhop_winterland",
      "bhop_treehouse2",
      "bhop_easyjump_daily2",
      "bhop_alt_vaahtera",
      "bhop_quaker",
    ],
    faq: 3,
  },
  {
    key: "2x2",
    cover: SCREENSHOT("de_inferno"),
    accent: "#3e8ed0",
    order: 40,
    slots: 4,
    commands: MAP_VOTE_COMMANDS,
    maps: ["Inferno", "Nuke", "Overpass", "Vertigo", "Poseidon"],
    faq: 2,
  },
];

export function serverModeDefinition(
  key: string,
): ServerModeDefinition | undefined {
  return SERVER_MODES.find((mode) => mode.key === key);
}

/** A server belongs to the section through its section_mode, set by the api. */
export function sectionModeKey(value: unknown): ServerModeKey | null {
  return value === "duels" ||
    value === "dm" ||
    value === "bhop" ||
    value === "2x2"
    ? value
    : null;
}

/** The api labels section servers "Duels #3"; players call that one #3. */
export function serverNumber(label: string | null | undefined): number | null {
  const match = /#(\d+)\s*$/.exec(label || "");
  return match ? Number(match[1]) : null;
}

/** Workshop maps report as workshop/<id>/<name>; the card wants <name>. */
export function liveMapName(raw: string | null | undefined): string {
  const value = String(raw || "").trim();
  if (!value || value === "unknown") return "default";
  return (
    value
      .replace(/^workshop\//, "")
      .split("/")
      .pop() || "default"
  );
}

const MAP_LABELS: Record<string, string> = {
  am_mirage_middle: "Mirage Duels",
  am_anubis_p: "Anubis Duels",
  am_redline: "Redline",
  am_redline_ngnw: "Redline NGNW",
};

/** The name a player knows a map by: "Mirage Duels", "Inferno", bhop_colour. */
export function mapLabel(map: string): string {
  if (!map || map === "default") return "—";
  if (MAP_LABELS[map]) return MAP_LABELS[map];
  if (/^(de|cs|ar)_/.test(map)) return cleanMapName(map);
  return map;
}
