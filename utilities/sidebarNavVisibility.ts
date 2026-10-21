/** Public LeftNav items an admin can hide from regular players. */
export const SIDEBAR_NAV_ITEMS = [
  { key: "search", labelKey: "layouts.app_nav.navigation.search" },
  { key: "dashboard", labelKey: "layouts.app_nav.navigation.dashboard" },
  { key: "play", labelKey: "layouts.app_nav.navigation.play" },
  { key: "watch", labelKey: "layouts.app_nav.navigation.watch" },
  { key: "tournaments", labelKey: "layouts.app_nav.navigation.tournaments" },
  { key: "leagues", labelKey: "layouts.app_nav.navigation.leagues" },
  { key: "events", labelKey: "layouts.app_nav.navigation.events" },
  { key: "game_servers", labelKey: "layouts.app_nav.navigation.game_servers" },
  { key: "hosting", labelKey: "layouts.app_nav.navigation.hosting" },
  { key: "teamspeak", labelKey: "layouts.app_nav.navigation.teamspeak" },
  { key: "store", labelKey: "layouts.app_nav.navigation.store" },
  { key: "battle_pass", labelKey: "layouts.app_nav.navigation.battle_pass" },
  { key: "utility", labelKey: "layouts.app_nav.navigation.utility" },
  { key: "plugins", labelKey: "layouts.app_nav.plugins.title" },
  { key: "players", labelKey: "layouts.app_nav.navigation.players" },
  { key: "teams", labelKey: "layouts.app_nav.navigation.teams" },
  { key: "clubs", labelKey: "layouts.app_nav.navigation.clubs" },
  { key: "scrims", labelKey: "layouts.app_nav.navigation.scrims" },
  { key: "leaderboard", labelKey: "layouts.app_nav.navigation.leaderboard" },
  { key: "news", labelKey: "layouts.app_nav.navigation.news" },
  { key: "highlights", labelKey: "layouts.app_nav.navigation.highlights" },
] as const;

export type SidebarNavKey = (typeof SIDEBAR_NAV_ITEMS)[number]["key"];

export const SIDEBAR_ADMIN_ONLY_SETTING = "public.sidebar_admin_only";

export function parseSidebarAdminOnly(raw: string | undefined | null): string[] {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((item): item is string => typeof item === "string");
  } catch {
    return [];
  }
}
