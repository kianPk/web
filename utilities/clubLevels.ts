export const CLUB_PERM_KEYS = [
  "post_news",
  "moderate_news",
  "manage_members",
  "manage_invites",
  "manage_requests",
  "manage_bans",
  "manage_servers",
  "manage_challenges",
  "manage_settings",
  "manage_levels",
  "set_titles",
  "set_staff_level",
] as const;

export type ClubPermKey = (typeof CLUB_PERM_KEYS)[number];

export const DEFAULT_LEVEL_PERMISSIONS: Record<ClubPermKey, { min: number }> = {
  post_news: { min: 8 },
  moderate_news: { min: 8 },
  manage_members: { min: 8 },
  manage_invites: { min: 8 },
  manage_requests: { min: 8 },
  manage_bans: { min: 8 },
  manage_servers: { min: 8 },
  manage_challenges: { min: 8 },
  manage_settings: { min: 9 },
  manage_levels: { min: 10 },
  set_titles: { min: 10 },
  set_staff_level: { min: 10 },
};

export const CLUB_XP_THRESHOLDS: Record<number, number> = {
  2: 150,
  3: 400,
  4: 800,
  5: 1500,
  6: 3000,
};

export function clubLevelLabel(level: number, t: (key: string) => string) {
  const key = `pages.clubs.levels.${level}`;
  const out = t(key);
  return out === key ? `L${level}` : out;
}
