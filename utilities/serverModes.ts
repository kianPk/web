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

/** A server belongs to the section through its section_mode, set by the api. */
export function sectionModeKey(value: unknown): ServerModeKey | null {
  return value === "duels" || value === "2x2" || value === "awp" ? value : null;
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

/** The name a player knows a map by: "Mirage Duels", "Inferno", awp_lego_2. */
export function mapLabel(map: string): string {
  if (!map || map === "default") return "—";
  if (MAP_LABELS[map]) return MAP_LABELS[map];
  if (/^(de|cs|ar)_/.test(map)) return cleanMapName(map);
  return map;
}
