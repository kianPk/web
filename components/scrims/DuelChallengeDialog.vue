<script lang="ts">
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Coins, Swords, X } from "lucide-vue-next";
import PlayerSearch from "~/components/PlayerSearch.vue";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import DateTimePicker from "~/components/common/DateTimePicker.vue";
import { useApplicationSettingsStore } from "~/stores/ApplicationSettings";
import { useAuthStore } from "~/stores/AuthStore";
import { useYpoints } from "~/composables/useYpoints";
import mapLabel from "~/utilities/mapLabel";
import {
  tacticalSectionLabelClasses,
  tacticalSectionTickClasses,
} from "~/utilities/tacticalClasses";
import {
  DUEL_MAP_POOLS_QUERY,
  SEND_DUEL_MUTATION,
} from "~/graphql/duelGraphql";

export default {
  components: {
    Button,
    Input,
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogFooter,
    Coins,
    Swords,
    X,
    PlayerSearch,
    PlayerDisplay,
    DateTimePicker,
  },
  props: {
    open: {
      type: Boolean,
      default: false,
    },
    opponent: {
      type: Object as () => any,
      default: null,
    },
  },
  emits: ["update:open", "sent"],
  setup() {
    const { balance } = useYpoints();
    return { ypointBalance: balance };
  },
  data() {
    return {
      picked: null as any,
      when: "now" as "now" | "later",
      scheduledAt: "" as string,
      bestOf: 1,
      mapId: "" as string,
      maps: [] as any[],
      wager: "0",
      wagerPresets: [0, 25, 50, 100, 250],
      submitting: false,
      sectionLabelClasses: tacticalSectionLabelClasses,
      sectionTickClasses: tacticalSectionTickClasses,
    };
  },
  computed: {
    mySteamId(): string | undefined {
      return useAuthStore().me?.steam_id;
    },
    target(): any {
      return this.picked ?? this.opponent;
    },
    wagersEnabled(): boolean {
      return (
        useApplicationSettingsStore().settings.find(
          (s: { name: string }) => s.name === "public.scrim_wager_enabled",
        )?.value !== "false"
      );
    },
    wagerRakePercent(): number {
      const raw = useApplicationSettingsStore().settings.find(
        (s: { name: string }) => s.name === "public.scrim_wager_rake_percent",
      )?.value;
      const value = Math.floor(Number(raw));
      return raw && Number.isFinite(value)
        ? Math.min(50, Math.max(0, value))
        : 5;
    },
    wagerAmount(): number {
      const value = Number(this.wager);
      return Number.isInteger(value) && value > 0 ? value : 0;
    },
    wagerInvalid(): boolean {
      const value = Number(this.wager);
      return this.wager !== "" && (!Number.isInteger(value) || value < 0);
    },
    wagerTooHigh(): boolean {
      return (
        this.ypointBalance !== null && this.wagerAmount > this.ypointBalance
      );
    },
    winnerPayout(): number {
      const pot = this.wagerAmount * 2;
      return pot - Math.floor((pot * this.wagerRakePercent) / 100);
    },
    scheduleInvalid(): boolean {
      if (this.when === "now") {
        return false;
      }
      const at = new Date(this.scheduledAt).getTime();
      return !this.scheduledAt || Number.isNaN(at) || at <= Date.now();
    },
    canSend(): boolean {
      return (
        !!this.target &&
        String(this.target.steam_id) !== String(this.mySteamId) &&
        !this.scheduleInvalid &&
        !this.wagerInvalid &&
        !this.wagerTooHigh
      );
    },
  },
  watch: {
    open(value: boolean) {
      if (!value) {
        return;
      }
      this.picked = null;
      this.when = "now";
      this.scheduledAt = "";
      this.bestOf = 1;
      this.mapId = "";
      this.wager = "0";
      if (this.maps.length === 0) {
        this.loadMaps();
      }
    },
    bestOf(value: number) {
      if (value !== 1) {
        this.mapId = "";
      }
    },
  },
  methods: {
    cleanName(map: any): string {
      return mapLabel(map);
    },
    async loadMaps() {
      try {
        const { data } = await this.$apollo.query({
          query: DUEL_MAP_POOLS_QUERY,
        });
        const pools = [...(data?.map_pools ?? [])].sort(
          (a: any, b: any) => Number(b.seed) - Number(a.seed),
        );
        this.maps =
          pools.find((pool: any) => (pool.maps?.length ?? 0) > 0)?.maps ?? [];
      } catch {
        this.maps = [];
      }
    },
    async send() {
      if (!this.canSend) {
        return;
      }
      this.submitting = true;
      try {
        await this.$apollo.mutate({
          mutation: SEND_DUEL_MUTATION,
          variables: {
            toSteamId: String(this.target.steam_id),
            bestOf: this.bestOf,
            mapId: this.mapId || null,
            proposedScheduledAt:
              this.when === "later"
                ? new Date(this.scheduledAt).toISOString()
                : null,
            wagerYpoints:
              this.wagersEnabled && this.wagerAmount > 0
                ? this.wagerAmount
                : null,
          },
        });
        toast({ title: this.$t("scrim.duel.sent") });
        this.$emit("sent");
        this.$emit("update:open", false);
      } catch (error) {
        toast({
          title: this.$t("scrim.duel.send_error"),
          description: error instanceof Error ? error.message : undefined,
          variant: "destructive",
        });
      } finally {
        this.submitting = false;
      }
    },
  },
};
</script>

<template>
  <Dialog :open="open" @update:open="(value) => $emit('update:open', value)">
    <DialogContent class="max-w-xl">
      <DialogHeader>
        <DialogTitle class="flex items-center gap-2">
          <Swords class="h-5 w-5 text-[hsl(var(--tac-amber))]" />
          {{ $t("scrim.duel.challenge_title") }}
        </DialogTitle>
      </DialogHeader>

      <div class="min-w-0 space-y-5">
        <div class="space-y-2">
          <span :class="sectionLabelClasses">
            <span :class="sectionTickClasses" />
            {{ $t("scrim.duel.opponent") }}
          </span>
          <div
            v-if="target"
            class="flex items-center justify-between gap-3 rounded-md border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.07)] px-3 py-2"
          >
            <PlayerDisplay :player="target" size="sm" />
            <button
              v-if="!opponent || picked"
              type="button"
              class="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
              @click="picked = null"
            >
              <X class="h-3.5 w-3.5" />
              {{ $t("scrim.duel.change_opponent") }}
            </button>
          </div>
          <PlayerSearch
            v-else
            :label="$t('scrim.duel.search_player')"
            :exclude="mySteamId ? [String(mySteamId)] : []"
            :selected="picked"
            @selected="(player: any) => (picked = player)"
          />
        </div>

        <div class="space-y-2">
          <span :class="sectionLabelClasses">
            <span :class="sectionTickClasses" />
            {{ $t("scrim.duel.when") }}
          </span>
          <div class="flex gap-2">
            <button
              v-for="option in ['now', 'later']"
              :key="option"
              type="button"
              class="flex-1 rounded-md border px-3 py-1.5 text-sm transition-colors"
              :class="
                when === option
                  ? 'border-[hsl(var(--tac-amber)/0.5)] bg-[hsl(var(--tac-amber)/0.1)] text-[hsl(var(--tac-amber))]'
                  : 'border-border text-muted-foreground hover:bg-muted/40'
              "
              @click="when = option as 'now' | 'later'"
            >
              {{ $t(`scrim.duel.${option}`) }}
            </button>
          </div>
          <DateTimePicker v-if="when === 'later'" v-model="scheduledAt" size="sm" />
          <p class="text-xs text-muted-foreground">
            {{
              when === "now"
                ? $t("scrim.duel.now_help")
                : $t("scrim.duel.later_help")
            }}
          </p>
        </div>

        <div class="space-y-2">
          <span :class="sectionLabelClasses">
            <span :class="sectionTickClasses" />
            {{ $t("scrim.series") }}
          </span>
          <div class="flex gap-2">
            <button
              v-for="option in [1, 3, 5]"
              :key="option"
              type="button"
              class="flex-1 rounded-md border px-3 py-1.5 text-sm transition-colors"
              :class="
                bestOf === option
                  ? 'border-[hsl(var(--tac-amber)/0.5)] bg-[hsl(var(--tac-amber)/0.1)] text-[hsl(var(--tac-amber))]'
                  : 'border-border text-muted-foreground hover:bg-muted/40'
              "
              @click="bestOf = option"
            >
              {{ $t("scrim.best_of", { option }) }}
            </button>
          </div>
        </div>

        <div v-if="bestOf === 1 && maps.length" class="space-y-2">
          <span :class="sectionLabelClasses">
            <span :class="sectionTickClasses" />
            {{ $t("scrim.duel.map") }}
          </span>
          <div class="flex flex-wrap gap-2">
            <button
              type="button"
              class="rounded-md border px-3 py-1.5 text-sm transition-colors"
              :class="
                !mapId
                  ? 'border-[hsl(var(--tac-amber)/0.5)] bg-[hsl(var(--tac-amber)/0.1)] text-[hsl(var(--tac-amber))]'
                  : 'border-border text-muted-foreground hover:bg-muted/40'
              "
              @click="mapId = ''"
            >
              {{ $t("scrim.duel.map_veto") }}
            </button>
            <button
              v-for="map in maps"
              :key="map.id"
              type="button"
              class="inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1.5 text-sm transition-colors"
              :class="
                mapId === map.id
                  ? 'border-[hsl(var(--tac-amber)/0.5)] bg-[hsl(var(--tac-amber)/0.1)] text-[hsl(var(--tac-amber))]'
                  : 'border-border text-muted-foreground hover:bg-muted/40'
              "
              @click="mapId = map.id"
            >
              <img
                v-if="map.patch"
                :src="map.patch"
                :alt="cleanName(map)"
                class="h-5 w-5 object-contain"
              />
              {{ cleanName(map) }}
            </button>
          </div>
        </div>

        <div v-if="wagersEnabled" class="space-y-2">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <span :class="sectionLabelClasses">
              <span :class="sectionTickClasses" />
              {{ $t("scrim.wager.title") }}
            </span>
            <span
              v-if="ypointBalance !== null"
              class="font-mono text-[0.65rem] uppercase tracking-[0.14em] text-muted-foreground"
            >
              {{ $t("scrim.wager.your_balance", { balance: ypointBalance }) }}
            </span>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <button
              v-for="preset in wagerPresets"
              :key="preset"
              type="button"
              class="rounded-md border px-3 py-1.5 text-sm tabular-nums transition-colors"
              :class="
                wagerAmount === preset
                  ? 'border-[hsl(var(--tac-amber)/0.5)] bg-[hsl(var(--tac-amber)/0.1)] text-[hsl(var(--tac-amber))]'
                  : 'border-border text-muted-foreground hover:bg-muted/40'
              "
              @click="wager = String(preset)"
            >
              {{ preset === 0 ? $t("scrim.wager.none") : preset }}
            </button>
            <div class="relative">
              <Coins
                class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[hsl(var(--tac-amber))]"
              />
              <Input
                v-model="wager"
                type="number"
                min="0"
                step="1"
                inputmode="numeric"
                class="h-8 w-[120px] pl-8 tabular-nums"
                :aria-label="$t('scrim.wager.amount_label')"
              />
            </div>
          </div>
          <div
            v-if="wagerAmount > 0"
            class="flex items-start gap-2 rounded-md border border-[hsl(var(--tac-amber)/0.3)] bg-[hsl(var(--tac-amber)/0.08)] px-3 py-2"
          >
            <Coins class="mt-0.5 h-4 w-4 shrink-0 text-[hsl(var(--tac-amber))]" />
            <div class="space-y-1 text-xs text-muted-foreground">
              <p class="text-sm font-semibold text-foreground">
                {{
                  $t("scrim.duel.wager_summary", {
                    amount: wagerAmount,
                    payout: winnerPayout,
                  })
                }}
              </p>
              <p>{{ $t("scrim.duel.wager_rules", { rake: wagerRakePercent }) }}</p>
            </div>
          </div>
          <p v-if="wagerInvalid" class="text-xs text-destructive">
            {{ $t("scrim.wager.invalid") }}
          </p>
          <p v-else-if="wagerTooHigh" class="text-xs text-destructive">
            {{ $t("scrim.duel.not_enough", { balance: ypointBalance }) }}
          </p>
        </div>
      </div>

      <DialogFooter>
        <Button
          class="tac-amber-cta"
          :loading="submitting"
          :disabled="!canSend"
          @click="send"
        >
          <Swords class="h-4 w-4" />
          {{ $t("scrim.duel.send") }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
