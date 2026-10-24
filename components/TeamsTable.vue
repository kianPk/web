<script setup lang="ts">
import { Avatar, AvatarImage, AvatarFallback } from "~/components/ui/avatar";
import TimezoneFlag from "~/components/TimezoneFlag.vue";
import TeamsAwardShelf from "~/components/teams/TeamsAwardShelf.vue";
import ClubTeamBadge from "~/components/clubs/ClubTeamBadge.vue";
import { Users } from "lucide-vue-next";
import { resolveRosterImageUrl } from "~/utilities/rosterImage";
import {
  memberClubsForTeam,
  officialClubForTeam,
  type TeamClub,
} from "~/utilities/teamClubs";
</script>

<template>
  <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
    <NuxtLink
      v-for="(team, index) in teams"
      :key="team.id"
      :to="{ name: 'teams-id', params: { id: team.id } }"
      class="group team-card relative flex flex-col gap-3 overflow-hidden rounded-lg border p-4 transition-all duration-200 animate-in fade-in slide-in-from-bottom-2"
      :class="
        officialClub(team)
          ? 'border-[hsl(var(--tac-amber)/0.65)] bg-[linear-gradient(180deg,hsl(var(--tac-amber)/0.16)_0%,hsl(var(--card)/0.7)_48%)] hover:border-[hsl(var(--tac-amber)/0.85)]'
          : 'border-border bg-card/50 hover:border-[hsl(var(--tac-amber)/0.45)] hover:bg-card/70'
      "
      :style="{ animationDelay: `${index * 40}ms` }"
    >
      <span
        aria-hidden="true"
        class="pointer-events-none absolute -left-[1px] -top-[1px] h-3 w-3 border-l-2 border-t-2 border-[hsl(var(--tac-amber)/0.55)] opacity-0 transition-opacity duration-200 group-hover:opacity-100"
      ></span>
      <span
        aria-hidden="true"
        class="pointer-events-none absolute -bottom-[1px] -right-[1px] h-3 w-3 border-b-2 border-r-2 border-[hsl(var(--tac-amber)/0.55)] opacity-0 transition-opacity duration-200 group-hover:opacity-100"
      ></span>

      <!-- Header: avatar + name + stats -->
      <div class="flex items-start gap-3 min-w-0">
        <div
          class="relative shrink-0 h-12 w-12 border border-[hsl(var(--tac-amber)/0.4)] bg-[hsl(var(--tac-amber)/0.1)] flex items-center justify-center overflow-hidden"
        >
          <img
            v-if="teamAvatarSrc(team)"
            :src="teamAvatarSrc(team)"
            :alt="team.name"
            class="h-full w-full object-cover"
          />
          <span
            v-else
            class="font-mono text-[0.65rem] font-bold uppercase tracking-[0.12em] text-[hsl(var(--tac-amber))]"
          >
            {{ (team.short_name || team.name || "?").slice(0, 3) }}
          </span>
        </div>
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2 min-w-0">
            <span
              class="truncate font-semibold text-base leading-tight"
              :title="team.name"
            >
              {{ team.name }}
            </span>
            <span
              v-if="team.short_name"
              class="shrink-0 inline-flex items-center border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.1)] px-1.5 py-0.5 font-mono text-[0.55rem] font-bold uppercase tracking-[0.18em] text-[hsl(var(--tac-amber))]"
            >
              {{ team.short_name }}
            </span>
          </div>
          <ClubTeamBadge
            class="mt-1.5"
            compact
            :official-club="officialClub(team)"
            :clubs="memberClubs(team)"
          />
          <div
            class="mt-1 flex items-center gap-1 text-[0.65rem] font-mono uppercase tracking-[0.18em] text-muted-foreground/80"
          >
            <Users class="w-3 h-3" />
            <span class="tabular-nums">{{ rosterCount(team) }}</span>
            <span>{{ $t("team.roster_short") }}</span>
            <template v-if="topCountries(team).length">
              <span class="mx-1 opacity-40">·</span>
              <span class="flex items-center gap-1">
                <TimezoneFlag
                  v-for="country in topCountries(team)"
                  :key="country"
                  :country="country"
                  class="h-3 w-4 shrink-0"
                />
              </span>
            </template>
          </div>
        </div>
      </div>

      <!-- Top 5 starters by ELO + recent awards -->
      <div v-if="topStarters(team).length" class="flex items-center gap-1.5">
        <div class="flex -space-x-1.5">
          <Avatar
            v-for="rosterItem in topStarters(team)"
            :key="rosterItem.player?.steam_id"
            class="h-7 w-7 rounded border-2 border-card shadow-sm"
            :title="rosterItem.player?.name"
          >
            <AvatarImage
              :src="rosterPlayerImage(rosterItem)"
              :alt="rosterItem.player?.name"
            />
            <AvatarFallback class="rounded text-[10px]">
              {{ rosterItem.player?.name?.slice(0, 2) ?? "?" }}
            </AvatarFallback>
          </Avatar>
        </div>
        <span
          v-if="extraCount(team) > 0"
          class="ml-1 inline-flex items-center justify-center rounded border border-border bg-muted/40 px-1.5 h-7 text-[0.65rem] font-mono tabular-nums text-muted-foreground"
        >
          +{{ extraCount(team) }}
        </span>

        <TeamsAwardShelf
          v-if="teamAwards(team).length"
          class="ml-auto"
          :awards="teamAwards(team)"
        />
      </div>

      <div v-else class="text-xs text-muted-foreground italic">
        {{ $t("team.table.no_roster") }}
      </div>
    </NuxtLink>
  </div>
</template>

<script lang="ts">
import type { TeamAwardEntry } from "~/components/teams/teamAwards";

interface RosterPlayer {
  elo?: {
    competitive?: number | null;
  } | null;
  country?: string | null;
  steam_id?: string;
  name?: string;
  avatar_url?: string;
  roster_image_url?: string | null;
}

interface RosterEntry {
  roster_image_url?: string | null;
  player?: RosterPlayer | null;
}

export default {
  props: {
    teams: {
      required: true,
      type: Object,
    },
    awardsByTeamId: {
      type: Object,
      default: () => ({}),
    },
    officialByTeamId: {
      type: Object,
      default: () => ({}),
    },
  },
  computed: {
    apiDomain() {
      return useRuntimeConfig().public.apiDomain;
    },
  },
  methods: {
    teamAvatarSrc(team: { avatar_url?: string | null }): string | null {
      if (!team.avatar_url) return null;
      return `https://${this.apiDomain}/${team.avatar_url}`;
    },
    rosterPlayerImage(rosterItem: RosterEntry): string | null {
      return (
        resolveRosterImageUrl(
          rosterItem,
          rosterItem.player ?? null,
          this.apiDomain,
        ) ??
        rosterItem.player?.avatar_url ??
        null
      );
    },
    rosterCount(team: { roster?: RosterEntry[] }): number {
      return team.roster?.length ?? 0;
    },
    teamAwards(team: { id: string }): TeamAwardEntry[] {
      return (
        (this.awardsByTeamId as Record<string, TeamAwardEntry[]>)[team.id] ?? []
      );
    },
    officialClub(team: any): TeamClub | null {
      return officialClubForTeam(
        team,
        this.officialByTeamId as Record<string, TeamClub>,
      );
    },
    memberClubs(team: any): TeamClub[] {
      return memberClubsForTeam(
        team,
        this.officialByTeamId as Record<string, TeamClub>,
      );
    },
    topStarters(team: { roster?: RosterEntry[] }): RosterEntry[] {
      const roster = team.roster ?? [];
      return [...roster]
        .sort((a, b) => {
          const aElo = a.player?.elo?.competitive ?? 0;
          const bElo = b.player?.elo?.competitive ?? 0;
          return bElo - aElo;
        })
        .slice(0, 5);
    },
    extraCount(team: { roster?: RosterEntry[] }): number {
      return Math.max(0, (team.roster?.length ?? 0) - 5);
    },
    avgElo(team: { roster?: RosterEntry[] }): number | null {
      const roster = this.topStarters(team);
      const values = roster
        .map((r) => r.player?.elo?.competitive)
        .filter((v): v is number => typeof v === "number" && v > 0);
      if (values.length === 0) return null;
      return Math.round(values.reduce((a, b) => a + b, 0) / values.length);
    },
    topCountries(team: { roster?: RosterEntry[] }): string[] {
      const counts = new Map<string, number>();
      for (const r of team.roster ?? []) {
        const c = r.player?.country;
        if (!c) continue;
        counts.set(c, (counts.get(c) ?? 0) + 1);
      }
      return [...counts.entries()]
        .sort((a, b) => b[1] - a[1])
        .slice(0, 3)
        .map(([c]) => c);
    },
  },
};
</script>
