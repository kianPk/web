<script lang="ts">
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Coins, Swords, Clock, Radio } from "lucide-vue-next";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import TimeAgo from "~/components/TimeAgo.vue";
import SectionEmpty from "~/components/common/SectionEmpty.vue";
import DuelChallengeDialog from "~/components/scrims/DuelChallengeDialog.vue";
import { useAuthStore } from "~/stores/AuthStore";
import { dateLocale } from "~/utilities/dateLocale";
import mapLabel from "~/utilities/mapLabel";
import {
  CANCEL_DUEL_MUTATION,
  DUEL_LISTINGS_SUBSCRIPTION,
  JOIN_DUEL_LIST_MUTATION,
  LEAVE_DUEL_LIST_MUTATION,
  MY_DUELS_SUBSCRIPTION,
  RESPOND_DUEL_MUTATION,
} from "~/graphql/duelGraphql";

const DONE_MATCH_STATUSES = [
  "Finished",
  "Tie",
  "Canceled",
  "Forfeit",
  "Surrendered",
];

export default {
  components: {
    Button,
    Input,
    Badge,
    Coins,
    Swords,
    Clock,
    Radio,
    PlayerDisplay,
    TimeAgo,
    SectionEmpty,
    DuelChallengeDialog,
  },
  data() {
    return {
      listings: [] as any[],
      listingsLoaded: false,
      duels: [] as any[],
      note: "",
      busy: false,
      busyRequest: "" as string,
      dialogOpen: false,
      dialogOpponent: null as any,
    };
  },
  computed: {
    mySteamId(): string | undefined {
      return useAuthStore().me?.steam_id;
    },
    myListing(): any {
      return this.listings.find(
        (listing: any) => String(listing.steam_id) === String(this.mySteamId),
      );
    },
    otherListings(): any[] {
      return this.listings.filter(
        (listing: any) => String(listing.steam_id) !== String(this.mySteamId),
      );
    },
    openDuels(): any[] {
      return this.duels.filter(
        (duel: any) =>
          duel.status === "Pending" ||
          (duel.match && !DONE_MATCH_STATUSES.includes(duel.match.status)),
      );
    },
  },
  apollo: {
    $subscribe: {
      listings: {
        query: DUEL_LISTINGS_SUBSCRIPTION,
        skip() {
          return !this.mySteamId;
        },
        result({ data }: { data: any }) {
          this.listings = data?.player_duel_listings ?? [];
          this.listingsLoaded = true;
        },
      },
      duels: {
        query: MY_DUELS_SUBSCRIPTION,
        variables() {
          return { steamId: this.mySteamId };
        },
        skip() {
          return !this.mySteamId;
        },
        result({ data }: { data: any }) {
          this.duels = data?.player_duel_requests ?? [];
        },
      },
    },
  },
  methods: {
    cleanName(map: any): string {
      return mapLabel(map);
    },
    isIncoming(duel: any): boolean {
      return String(duel.to_steam_id) === String(this.mySteamId);
    },
    opponentOf(duel: any): any {
      return this.isIncoming(duel) ? duel.from_player : duel.to_player;
    },
    duelElo(player: any): number | null {
      const elo = player?.elo?.duel;
      return typeof elo === "number" ? Math.round(elo) : null;
    },
    formatTime(iso: string | null): string {
      if (!iso) {
        return this.$t("scrim.duel.asap");
      }
      return new Date(iso).toLocaleString(dateLocale(), {
        weekday: "short",
        hour: "numeric",
        minute: "2-digit",
      });
    },
    openChallenge(opponent: any = null) {
      this.dialogOpponent = opponent;
      this.dialogOpen = true;
    },
    async joinList() {
      this.busy = true;
      try {
        await this.$apollo.mutate({
          mutation: JOIN_DUEL_LIST_MUTATION,
          variables: { note: this.note.trim() || null },
        });
      } catch (error) {
        this.fail(error);
      } finally {
        this.busy = false;
      }
    },
    async leaveList() {
      this.busy = true;
      try {
        await this.$apollo.mutate({
          mutation: LEAVE_DUEL_LIST_MUTATION,
          variables: { steamId: this.mySteamId },
        });
        this.note = "";
      } catch (error) {
        this.fail(error);
      } finally {
        this.busy = false;
      }
    },
    async respond(duel: any, accept: boolean) {
      if (
        accept &&
        duel.wager_ypoints > 0 &&
        !window.confirm(
          this.$t("scrim.duel.accept_confirm", { amount: duel.wager_ypoints }),
        )
      ) {
        return;
      }
      this.busyRequest = duel.id;
      try {
        await this.$apollo.mutate({
          mutation: RESPOND_DUEL_MUTATION,
          variables: { requestId: duel.id, accept },
        });
        toast({
          title: accept
            ? this.$t("scrim.duel.accepted")
            : this.$t("scrim.duel.declined"),
        });
      } catch (error) {
        this.fail(error);
      } finally {
        this.busyRequest = "";
      }
    },
    async cancel(duel: any) {
      if (
        duel.status === "Matched" &&
        duel.wager_status === "Escrowed" &&
        !window.confirm(this.$t("scrim.duel.cancel_confirm"))
      ) {
        return;
      }
      this.busyRequest = duel.id;
      try {
        await this.$apollo.mutate({
          mutation: CANCEL_DUEL_MUTATION,
          variables: { requestId: duel.id },
        });
        toast({
          title:
            duel.status === "Matched"
              ? this.$t("scrim.duel.canceled")
              : this.$t("scrim.duel.withdrawn"),
        });
      } catch (error) {
        this.fail(error);
      } finally {
        this.busyRequest = "";
      }
    },
    fail(error: unknown) {
      toast({
        title: this.$t("scrim.duel.action_error"),
        description: error instanceof Error ? error.message : undefined,
        variant: "destructive",
      });
    },
  },
};
</script>

<template>
  <SectionEmpty
    v-if="!mySteamId"
    :title="$t('scrim.duel.tab')"
    :description="$t('scrim.duel.sign_in')"
  />

  <div v-else class="space-y-8">
    <section
      class="relative overflow-hidden rounded-lg border border-[hsl(var(--tac-amber)/0.3)] [background:linear-gradient(110deg,hsl(var(--tac-amber)/0.10)_0%,hsl(var(--card)/0.4)_60%)] p-4 sm:p-5"
    >
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div class="min-w-0 space-y-1">
          <h2 class="flex items-center gap-2 text-lg font-semibold">
            <Radio
              class="h-4 w-4"
              :class="
                myListing
                  ? 'text-[hsl(var(--tac-amber))] animate-pulse'
                  : 'text-muted-foreground'
              "
            />
            {{
              myListing
                ? $t("scrim.duel.listing_on")
                : $t("scrim.duel.listing_title")
            }}
          </h2>
          <p class="max-w-xl text-sm text-muted-foreground">
            {{ $t("scrim.duel.listing_help") }}
          </p>
        </div>
        <Button class="tac-amber-cta shrink-0" @click="openChallenge()">
          <Swords class="h-4 w-4" />
          {{ $t("scrim.duel.challenge_player") }}
        </Button>
      </div>

      <div class="mt-4 flex flex-wrap items-center gap-2">
        <template v-if="!myListing">
          <Input
            v-model="note"
            maxlength="140"
            class="h-9 min-w-[14rem] flex-1 sm:max-w-sm"
            :placeholder="$t('scrim.duel.note_placeholder')"
            @keyup.enter="joinList"
          />
          <Button variant="secondary" :loading="busy" @click="joinList">
            {{ $t("scrim.duel.join") }}
          </Button>
        </template>
        <template v-else>
          <span
            v-if="myListing.note"
            class="rounded-md border border-border bg-card/60 px-3 py-1.5 text-sm"
          >
            {{ myListing.note }}
          </span>
          <Button variant="outline" :loading="busy" @click="leaveList">
            {{ $t("scrim.duel.leave") }}
          </Button>
        </template>
      </div>
    </section>

    <section class="space-y-3">
      <div class="flex items-center gap-3">
        <span
          class="text-[0.7rem] font-semibold uppercase tracking-[0.2em] text-[hsl(var(--tac-amber))]"
        >
          {{ $t("scrim.duel.your_challenges") }}
        </span>
        <span class="text-xs text-muted-foreground">{{ openDuels.length }}</span>
        <span class="h-px flex-1 bg-border" />
      </div>

      <p
        v-if="openDuels.length === 0"
        class="rounded-md border border-dashed border-border px-4 py-6 text-center text-sm text-muted-foreground"
      >
        {{ $t("scrim.duel.no_challenges") }}
      </p>

      <div
        v-for="duel in openDuels"
        :key="duel.id"
        class="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-lg border bg-card p-3"
        :class="
          duel.status === 'Pending' && isIncoming(duel)
            ? 'border-[hsl(var(--tac-amber)/0.6)]'
            : 'border-border'
        "
      >
        <div class="flex min-w-0 flex-1 flex-col gap-1.5">
          <div class="flex flex-wrap items-center gap-2 text-sm">
            <span class="text-muted-foreground">
              {{
                duel.status === "Matched"
                  ? $t("scrim.duel.on")
                  : isIncoming(duel)
                    ? $t("scrim.duel.incoming")
                    : $t("scrim.duel.outgoing")
              }}
            </span>
            <PlayerDisplay :player="opponentOf(duel)" size="xs" />
          </div>
          <div
            class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground"
          >
            <span class="inline-flex items-center gap-1">
              <Clock class="h-3.5 w-3.5" />
              {{ formatTime(duel.proposed_scheduled_at) }}
            </span>
            <span>{{ $t("scrim.best_of", { option: duel.best_of }) }}</span>
            <span v-if="duel.map" class="inline-flex items-center gap-1">
              <img
                v-if="duel.map.patch"
                :src="duel.map.patch"
                :alt="cleanName(duel.map)"
                class="h-4 w-4 object-contain"
              />
              {{ cleanName(duel.map) }}
            </span>
            <span
              v-if="duel.wager_ypoints > 0"
              class="inline-flex items-center gap-1 font-semibold text-[hsl(var(--tac-amber))]"
            >
              <Coins class="h-3.5 w-3.5" />
              {{ $t("scrim.duel.stake", { amount: duel.wager_ypoints }) }}
            </span>
            <span v-if="duel.status === 'Pending'" class="inline-flex gap-1">
              {{ $t("scrim.duel.expires") }}
              <TimeAgo :date="duel.expires_at" />
            </span>
          </div>
        </div>

        <div class="flex shrink-0 items-center gap-2">
          <template v-if="duel.status === 'Matched'">
            <Button
              v-if="duel.match_id"
              size="sm"
              class="tac-amber-cta"
              @click="$router.push(`/matches/${duel.match_id}`)"
            >
              {{ $t("scrim.duel.open_match") }}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              class="text-muted-foreground hover:text-destructive"
              :loading="busyRequest === duel.id"
              @click="cancel(duel)"
            >
              {{ $t("common.cancel") }}
            </Button>
          </template>
          <template v-else-if="isIncoming(duel)">
            <Button
              size="sm"
              class="tac-amber-cta"
              :loading="busyRequest === duel.id"
              @click="respond(duel, true)"
            >
              {{ $t("common.accept") }}
            </Button>
            <Button
              size="sm"
              variant="outline"
              :disabled="busyRequest === duel.id"
              @click="respond(duel, false)"
            >
              {{ $t("common.decline") }}
            </Button>
          </template>
          <Button
            v-else
            size="sm"
            variant="ghost"
            class="text-muted-foreground hover:text-destructive"
            :loading="busyRequest === duel.id"
            @click="cancel(duel)"
          >
            {{ $t("common.cancel") }}
          </Button>
        </div>
      </div>
    </section>

    <section class="space-y-3">
      <div class="flex items-center gap-3">
        <span
          class="text-[0.7rem] font-semibold uppercase tracking-[0.2em] text-[hsl(var(--tac-amber))]"
        >
          {{ $t("scrim.duel.players_title") }}
        </span>
        <span class="text-xs text-muted-foreground">{{
          otherListings.length
        }}</span>
        <span class="h-px flex-1 bg-border" />
      </div>

      <p
        v-if="listingsLoaded && otherListings.length === 0"
        class="rounded-md border border-dashed border-border px-4 py-6 text-center text-sm text-muted-foreground"
      >
        {{ $t("scrim.duel.no_players") }}
      </p>

      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <div
          v-for="listing in otherListings"
          :key="listing.steam_id"
          class="flex flex-col gap-3 rounded-lg border border-border bg-card p-4 transition-colors hover:border-[hsl(var(--tac-amber)/0.5)]"
        >
          <div class="flex items-start justify-between gap-2">
            <NuxtLink
              :to="`/players/${listing.steam_id}`"
              class="min-w-0 hover:text-[hsl(var(--tac-amber))]"
            >
              <PlayerDisplay :player="listing.player" size="sm" />
            </NuxtLink>
            <Badge
              v-if="duelElo(listing.player) !== null"
              variant="outline"
              class="shrink-0 tabular-nums"
              :title="$t('scrim.duel.duel_elo')"
            >
              {{ duelElo(listing.player) }}
            </Badge>
          </div>
          <p
            v-if="listing.note"
            class="line-clamp-2 text-sm text-muted-foreground"
          >
            {{ listing.note }}
          </p>
          <div class="mt-auto flex items-center justify-between gap-2">
            <TimeAgo
              :date="listing.created_at"
              class="text-xs text-muted-foreground"
            />
            <Button
              size="sm"
              class="tac-amber-cta"
              @click="openChallenge(listing.player)"
            >
              <Swords class="h-4 w-4" />
              {{ $t("scrim.duel.challenge") }}
            </Button>
          </div>
        </div>
      </div>
    </section>

    <DuelChallengeDialog
      v-model:open="dialogOpen"
      :opponent="dialogOpponent"
    />
  </div>
</template>
