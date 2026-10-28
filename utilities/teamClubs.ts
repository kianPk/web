export type TeamClub = {
  id: string;
  name: string;
  slug?: string | null;
  short_name?: string | null;
  avatar_url?: string | null;
  cover_url?: string | null;
  official_team_id?: string | null;
  is_primary?: boolean;
};

/** Nested owner → club_memberships selection for team list/detail queries. */
export const teamOwnerClubSelection = {
  steam_id: true,
  name: true,
  club_memberships: [
    {},
    {
      is_primary: true,
      club: {
        id: true,
        name: true,
        slug: true,
        short_name: true,
        avatar_url: true,
        cover_url: true,
        official_team_id: true,
      },
    },
  ],
};

export function clubsFromOwner(team: any): TeamClub[] {
  const rows = team?.owner?.club_memberships ?? [];
  const clubs: TeamClub[] = [];
  const seen = new Set<string>();
  const sorted = [...rows].sort(
    (a, b) => Number(!!b?.is_primary) - Number(!!a?.is_primary),
  );
  for (const row of sorted) {
    const club = row?.club;
    if (!club?.id || seen.has(club.id)) continue;
    seen.add(club.id);
    clubs.push({ ...club, is_primary: !!row.is_primary });
  }
  return clubs;
}

export function officialClubForTeam(
  team: any,
  officialByTeamId?: Record<string, TeamClub>,
): TeamClub | null {
  if (team?.id && officialByTeamId?.[team.id]) {
    return officialByTeamId[team.id];
  }
  return (
    clubsFromOwner(team).find((club) => club.official_team_id === team?.id) ??
    null
  );
}

export function memberClubsForTeam(
  team: any,
  officialByTeamId?: Record<string, TeamClub>,
): TeamClub[] {
  const official = officialClubForTeam(team, officialByTeamId);
  return clubsFromOwner(team).filter((club) => club.id !== official?.id);
}

export function indexOfficialClubs(
  rows: Array<TeamClub & { official_team_id?: string | null }>,
): Record<string, TeamClub> {
  const map: Record<string, TeamClub> = {};
  for (const row of rows) {
    if (row?.official_team_id) map[row.official_team_id] = row;
  }
  return map;
}
