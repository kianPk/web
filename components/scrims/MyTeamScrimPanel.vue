<script lang="ts">
import { $ } from "~/generated/zeus";
import { typedGql } from "~/generated/zeus/typedDocumentNode";
import { generateMutation } from "~/graphql/graphqlGen";
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Settings2, Users, Swords, Plus } from "lucide-vue-next";
import TeamScrimRequests from "~/components/team/TeamScrimRequests.vue";

export default {
  components: { Button, Settings2, Users, Swords, Plus, TeamScrimRequests },
  props: {
    teams: {
      type: Array as () => Array<{ id: string; name: string }>,
      default: () => [],
    },
  },
  emits: ["play-duel"],
  data() {
    return {
      settings: [] as any[],
      openRequests: [] as any[],
      selectedTeamId: "" as string,
      busyTeamId: "" as string,
    };
  },
  computed: {
    teamIds(): string[] {
      return this.teams.map((team) => team.id);
    },
    selectedTeam(): { id: string; name: string } | undefined {
      return (
        this.teams.find((team) => team.id === this.selectedTeamId) ??
        this.teams.at(0)
      );
    },
  },
  watch: {
    teamIds: {
      immediate: true,
      handler(ids: string[]) {
        if (!ids.includes(this.selectedTeamId)) {
          this.selectedTeamId = ids.at(0) ?? "";
        }
      },
    },
  },
  apollo: {
    $subscribe: {
      settings: {
        query: typedGql("subscription")({
          team_scrim_settings: [
            { where: { team_id: { _in: $("teamIds", "[uuid!]!") } } },
            { team_id: true, enabled: true },
          ],
        }),
        variables() {
          return { teamIds: this.teamIds };
        },
        skip() {
          return this.teamIds.length === 0;
        },
        result({ data }: { data: any }) {
          this.settings = data.team_scrim_settings ?? [];
        },
      },
      openRequests: {
        query: typedGql("subscription")({
          team_scrim_requests: [
            {
              where: {
                awaiting_team_id: { _in: $("teamIds", "[uuid!]!") },
                status: { _in: ["Pending", "Countered"] },
              },
            },
            { id: true, awaiting_team_id: true },
          ],
        }),
        variables() {
          return { teamIds: this.teamIds };
        },
        skip() {
          return this.teamIds.length === 0;
        },
        result({ data }: { data: any }) {
          this.openRequests = data.team_scrim_requests ?? [];
        },
      },
    },
  },
  methods: {
    isListed(teamId: string): boolean {
      return this.settings.some(
        (row: any) => row.team_id === teamId && row.enabled === true,
      );
    },
    waitingCount(teamId: string): number {
      return this.openRequests.filter(
        (request: any) => request.awaiting_team_id === teamId,
      ).length;
    },
    async setListed(teamId: string, enabled: boolean) {
      this.busyTeamId = teamId;
      try {
        // A first listing needs no schedule: anyone may propose any time and
        // the team accepts what suits it. The team page refines all of it.
        await this.$apollo.mutate({
          mutation: generateMutation({
            insert_team_scrim_settings_one: [
              {
                object: {
                  team_id: teamId,
                  enabled,
                  allow_outside_availability: true,
                },
                on_conflict: {
                  constraint: "team_scrim_settings_team_id_key",
                  update_columns: ["enabled"],
                },
              },
              { id: true },
            ],
          }),
        });
        toast({
          title: enabled
            ? this.$t("scrim.easy.listed_toast")
            : this.$t("scrim.easy.unlisted_toast"),
        });
      } catch (error) {
        toast({
          title: this.$t("scrim.settings_save_error"),
          description: error instanceof Error ? error.message : undefined,
          variant: "destructive",
        });
      } finally {
        this.busyTeamId = "";
      }
    },
  },
};
</script>

<template>
  <section
    v-if="teams.length === 0"
    class="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-dashed border-border/60 bg-card/40 p-4 sm:p-5"
  >
    <div class="flex min-w-0 items-start gap-3">
      <div
        class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-[rgba(255,85,0,0.12)] text-[#ff6a1f]"
      >
        <Users class="h-5 w-5" />
      </div>
      <div class="min-w-0 space-y-1">
        <h2 class="text-base font-semibold tracking-tight">
          {{ $t("scrim.easy.no_team_title") }}
        </h2>
        <p class="max-w-xl text-sm text-muted-foreground">
          {{ $t("scrim.easy.no_team_help") }}
        </p>
      </div>
    </div>
    <div class="flex flex-wrap gap-2">
      <Button variant="outline" @click="$router.push('/teams/create')">
        <Plus class="h-4 w-4" />
        {{ $t("scrim.easy.create_team") }}
      </Button>
      <Button class="tac-amber-cta" @click="$emit('play-duel')">
        <Swords class="h-4 w-4" />
        {{ $t("scrim.easy.try_duel") }}
      </Button>
    </div>
  </section>

  <section
    v-else
    class="space-y-4 rounded-xl border border-border/70 bg-card/40 p-4 sm:p-5"
  >
    <div class="flex items-start gap-3">
      <div
        class="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-[rgba(255,85,0,0.12)] text-[#ff6a1f]"
      >
        <Users class="h-5 w-5" />
      </div>
      <div>
        <h2 class="text-base font-semibold tracking-tight">
          {{ $t("scrim.easy.your_teams") }}
        </h2>
      </div>
    </div>

    <div class="space-y-2">
      <div
        v-for="team in teams"
        :key="team.id"
        class="flex flex-wrap items-center gap-3 rounded-xl border px-3 py-2.5 transition-colors"
        :class="
          selectedTeam?.id === team.id
            ? 'border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.06)]'
            : 'border-border/60 bg-background/30'
        "
      >
        <button
          type="button"
          class="flex min-w-0 flex-1 items-center gap-2 text-left"
          @click="selectedTeamId = team.id"
        >
          <span
            class="h-2 w-2 shrink-0 rounded-full"
            :class="
              isListed(team.id)
                ? 'bg-[hsl(var(--tac-amber))]'
                : 'bg-muted-foreground/50'
            "
          />
          <span class="truncate font-semibold">{{ team.name }}</span>
          <span
            class="rounded-full border border-border/60 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground"
          >
            {{
              isListed(team.id)
                ? $t("scrim.easy.listed")
                : $t("scrim.easy.not_listed")
            }}
          </span>
          <span
            v-if="waitingCount(team.id) > 0"
            class="inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-[hsl(var(--tac-amber))] px-1.5 text-[0.65rem] font-bold text-[hsl(var(--tac-amber-foreground))]"
            :title="$t('scrim.easy.waiting_on_you')"
          >
            {{ waitingCount(team.id) }}
          </span>
        </button>
        <div class="flex items-center gap-2">
          <Button
            v-if="!isListed(team.id)"
            size="sm"
            class="tac-amber-cta"
            :loading="busyTeamId === team.id"
            @click="setListed(team.id, true)"
          >
            {{ $t("scrim.easy.list_now") }}
          </Button>
          <Button
            v-else
            size="sm"
            variant="outline"
            :loading="busyTeamId === team.id"
            @click="setListed(team.id, false)"
          >
            {{ $t("scrim.easy.unlist") }}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            :title="$t('scrim.easy.settings')"
            @click="$router.push(`/teams/${team.id}?tab=scrim`)"
          >
            <Settings2 class="h-4 w-4" />
            <span class="hidden sm:inline">{{ $t("scrim.easy.settings") }}</span>
          </Button>
        </div>
      </div>
    </div>

    <div v-if="selectedTeam" class="space-y-2 border-t border-border/60 pt-4">
      <div class="text-sm font-semibold">
        {{ $t("scrim.easy.requests_title", { team: selectedTeam.name }) }}
      </div>
      <TeamScrimRequests :team-id="selectedTeam.id" compact />
    </div>
  </section>
</template>
