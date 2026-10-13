<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  CalendarClock,
  Crown,
  Gift,
  RefreshCw,
  ShoppingBag,
  Sparkles,
  Swords,
} from "lucide-vue-next";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import BattlePassRewardCell from "~/components/battle-pass/BattlePassRewardCell.vue";
import type { BattlePassReward } from "~/components/battle-pass/types";
import { Button } from "~/components/ui/button";
import { Skeleton } from "~/components/ui/skeleton";
import { Badge } from "~/components/ui/badge";
import { toast } from "~/components/ui/toast";
import { useAuthStore } from "~/stores/AuthStore";
import { useYpoints } from "~/composables/useYpoints";
import { formatTomanAmount } from "~/utilities/irrToman";

type Mission = {
  key: string;
  xp: number;
  target: number;
  progress: number;
  completed: boolean;
};

type Challenge = {
  id: string;
  key: string;
  target: number;
  progress: number;
  reward_ypoints: number;
  completed: boolean;
};

type BattlePassResponse = {
  season: {
    number: number;
    starts_at: string;
    ends_at: string;
    premium_price_ypoints: number;
  } | null;
  xp: number;
  level: number;
  xp_per_level: number;
  max_level: number;
  premium: boolean;
  balance: number;
  breakdown: {
    matches: number;
    daily: number;
    weekly: number;
    subscriber: number;
  };
  levels: Array<{
    level: number;
    free: BattlePassReward | null;
    premium: BattlePassReward | null;
    free_claimed: boolean;
    premium_claimed: boolean;
  }>;
  daily: { day: string; resets_at: string; missions: Mission[] };
  weekly: { week_start: string; resets_at: string; missions: Mission[] };
  subscriber: {
    unlocked: boolean;
    subscription: { tier: string; expires_at: string | null } | null;
    xp_per_challenge: number;
    challenges: Challenge[];
    products: Array<{
      id: string;
      title: string;
      price_irr: number;
      subscription_tier: string;
    }>;
  } | null;
};

const { t, locale } = useI18n();
const auth = useAuthStore();
const { refresh: refreshYpoints } = useYpoints();
const apiDomain = useRuntimeConfig().public.apiDomain as string;

const loading = ref(true);
const data = ref<BattlePassResponse | null>(null);
const busyCell = ref<string | null>(null);
const claimingAll = ref(false);
const buying = ref(false);
const track = ref<HTMLElement | null>(null);

const numberLocale = computed(() =>
  locale.value?.startsWith("fa") ? "fa-IR" : "en-US",
);

const season = computed(() => data.value?.season ?? null);

const levelXp = computed(() => {
  if (!data.value) return 0;
  if (data.value.level >= data.value.max_level) return data.value.xp_per_level;
  return data.value.xp % data.value.xp_per_level;
});

const levelPct = computed(() =>
  data.value
    ? Math.min(100, Math.round((levelXp.value / data.value.xp_per_level) * 100))
    : 0,
);

const claimableCount = computed(() => {
  if (!data.value) return 0;
  let count = 0;
  for (const row of data.value.levels) {
    if (row.level > data.value.level) break;
    if (row.free && !row.free_claimed) count++;
    if (data.value.premium && row.premium && !row.premium_claimed) count++;
  }
  return count;
});

const canAffordPremium = computed(
  () =>
    !!data.value?.season &&
    data.value.balance >= data.value.season.premium_price_ypoints,
);

function api(path: string) {
  return `https://${apiDomain}/battle-pass/${path}`;
}

function errorMessage(error: any) {
  return error?.data?.message || error?.message || String(error);
}

async function refresh() {
  if (!auth.me?.steam_id) {
    data.value = null;
    loading.value = false;
    return;
  }
  loading.value = true;
  try {
    data.value = await $fetch<BattlePassResponse>(api("me"), {
      credentials: "include",
      query: { _: Date.now() },
    });
    await nextTick();
    scrollToCurrent();
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: errorMessage(error),
    });
    data.value = null;
  } finally {
    loading.value = false;
  }
}

function scrollToCurrent() {
  if (!track.value || !data.value) return;
  const target = Math.min(data.value.level + 1, data.value.max_level);
  const column = track.value.querySelector<HTMLElement>(
    `[data-level="${target}"]`,
  );
  if (column) {
    track.value.scrollLeft =
      column.offsetLeft - track.value.clientWidth / 2 + column.clientWidth / 2;
  }
}

async function claim(level: number, trackName: "free" | "premium") {
  busyCell.value = `${level}:${trackName}`;
  try {
    await $fetch(api("claim"), {
      method: "POST",
      credentials: "include",
      body: { level, track: trackName },
    });
    toast({ title: t("pages.battle_pass.claimed_toast") });
    await Promise.all([refresh(), refreshYpoints()]);
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: errorMessage(error),
    });
  } finally {
    busyCell.value = null;
  }
}

async function claimAll() {
  claimingAll.value = true;
  try {
    const result = await $fetch<{ claimed: number }>(api("claim-all"), {
      method: "POST",
      credentials: "include",
    });
    toast({
      title: t("pages.battle_pass.claimed_all_toast", {
        count: result.claimed,
      }),
    });
    await Promise.all([refresh(), refreshYpoints()]);
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: errorMessage(error),
    });
  } finally {
    claimingAll.value = false;
  }
}

async function buyPremium() {
  if (!season.value) return;
  if (
    !window.confirm(
      t("pages.battle_pass.premium_confirm", {
        price: season.value.premium_price_ypoints,
      }),
    )
  ) {
    return;
  }
  buying.value = true;
  try {
    await $fetch(api("premium"), { method: "POST", credentials: "include" });
    toast({ title: t("pages.battle_pass.premium_toast") });
    await Promise.all([refresh(), refreshYpoints()]);
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: errorMessage(error),
    });
  } finally {
    buying.value = false;
  }
}

function formatIn(iso: string) {
  const diffMs = new Date(iso).getTime() - Date.now();
  const format = new Intl.RelativeTimeFormat(numberLocale.value, {
    numeric: "auto",
  });
  const minutes = Math.max(1, Math.round(diffMs / 60_000));
  if (minutes < 60) return format.format(minutes, "minute");
  const hours = Math.round(minutes / 60);
  if (hours < 48) return format.format(hours, "hour");
  return format.format(Math.round(hours / 24), "day");
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(numberLocale.value).format(value);
}

function missionPct(mission: { progress: number; target: number }) {
  if (mission.target <= 0) return 0;
  return Math.min(100, Math.round((mission.progress / mission.target) * 100));
}

function missionTitle(key: string, target: number) {
  return t(`pages.battle_pass.missions.${key}`, { target });
}

function formatPrice(irr: number) {
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale.value),
  });
}

onMounted(() => {
  void refresh();
});
</script>

<template>
  <PageTransition>
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 pb-16 pt-6">
      <TacticalPageHeader>
        <template #title>{{ $t("pages.battle_pass.title") }}</template>
        <template #subtitle>
          <span v-if="season">
            {{
              $t("pages.battle_pass.subtitle", {
                season: season.number,
                ends: formatIn(season.ends_at),
              })
            }}
          </span>
          <span v-else>{{ $t("pages.battle_pass.description") }}</span>
        </template>
        <template #actions>
          <Button
            variant="outline"
            size="sm"
            class="gap-2"
            :disabled="loading"
            @click="refresh"
          >
            <RefreshCw class="h-4 w-4" :class="{ 'animate-spin': loading }" />
            {{ $t("pages.battle_pass.refresh") }}
          </Button>
        </template>
      </TacticalPageHeader>

      <div v-if="!auth.me" class="text-sm text-muted-foreground">
        {{ $t("pages.battle_pass.login_required") }}
      </div>

      <div v-else-if="loading && !data" class="space-y-4">
        <Skeleton class="h-36 w-full" />
        <Skeleton class="h-64 w-full" />
      </div>

      <div
        v-else-if="data && !data.season"
        class="rounded-lg border border-border/60 bg-muted/20 p-6 text-sm text-muted-foreground"
      >
        {{ $t("pages.battle_pass.no_season") }}
      </div>

      <template v-else-if="data && season">
        <div class="grid gap-4 lg:grid-cols-[1fr_340px]">
          <div
            class="relative overflow-hidden rounded-xl border border-[hsl(var(--tac-amber)/0.35)] bg-gradient-to-br from-[hsl(var(--tac-amber)/0.12)] via-background to-violet-500/10 p-6"
          >
            <div class="flex flex-wrap items-end justify-between gap-4">
              <div class="flex items-center gap-4">
                <div
                  class="flex h-20 w-20 flex-col items-center justify-center rounded-lg border-2 border-[hsl(var(--tac-amber))] bg-background/60"
                >
                  <span
                    class="text-[10px] uppercase tracking-widest text-muted-foreground"
                  >
                    {{ $t("pages.battle_pass.level") }}
                  </span>
                  <span class="text-3xl font-black tabular-nums">
                    {{ data.level }}
                  </span>
                </div>
                <div>
                  <div class="text-lg font-bold">
                    {{
                      $t("pages.battle_pass.season_name", {
                        season: season.number,
                      })
                    }}
                  </div>
                  <div class="text-sm text-muted-foreground">
                    {{
                      $t("pages.battle_pass.total_xp", {
                        xp: formatNumber(data.xp),
                      })
                    }}
                  </div>
                </div>
              </div>
              <Badge
                v-if="data.premium"
                class="gap-1 bg-violet-500/90 text-white hover:bg-violet-500/90"
              >
                <Crown class="h-3.5 w-3.5" />
                {{ $t("pages.battle_pass.premium_active") }}
              </Badge>
            </div>

            <div class="mt-5 space-y-1.5">
              <div
                class="flex justify-between font-mono text-xs text-muted-foreground"
              >
                <span v-if="data.level < data.max_level">
                  {{
                    $t("pages.battle_pass.next_level", {
                      level: data.level + 1,
                    })
                  }}
                </span>
                <span v-else>{{ $t("pages.battle_pass.max_level") }}</span>
                <span class="tabular-nums">
                  {{ formatNumber(levelXp) }} /
                  {{ formatNumber(data.xp_per_level) }} XP
                </span>
              </div>
              <div class="h-3 overflow-hidden rounded-full bg-muted">
                <div
                  class="h-full rounded-full bg-gradient-to-r from-[hsl(var(--tac-amber))] to-violet-500 transition-all"
                  :style="{ width: `${levelPct}%` }"
                />
              </div>
            </div>

            <div class="mt-4 flex flex-wrap gap-2 text-xs">
              <span
                v-for="part in ['matches', 'daily', 'weekly', 'subscriber']"
                :key="part"
                class="rounded-md border border-border/60 bg-background/50 px-2 py-1"
              >
                {{ $t(`pages.battle_pass.breakdown.${part}`) }}:
                <span class="font-semibold tabular-nums">
                  {{ formatNumber(data.breakdown[part as keyof typeof data.breakdown]) }}
                </span>
              </span>
            </div>
          </div>

          <div
            class="flex flex-col gap-3 rounded-xl border p-5"
            :class="
              data.premium
                ? 'border-violet-500/40 bg-violet-500/5'
                : 'border-border/60 bg-muted/10'
            "
          >
            <div class="flex items-center gap-2 font-semibold">
              <Crown class="h-5 w-5 text-violet-400" />
              {{ $t("pages.battle_pass.premium_title") }}
            </div>
            <p class="text-sm text-muted-foreground">
              {{ $t("pages.battle_pass.premium_body") }}
            </p>
            <template v-if="!data.premium">
              <div class="flex items-center justify-between text-sm">
                <span class="text-muted-foreground">
                  {{ $t("pages.battle_pass.price") }}
                </span>
                <span class="inline-flex items-center gap-1 font-semibold">
                  <img
                    src="/img/ypoint-logo.png"
                    alt=""
                    class="h-4 w-4 object-contain"
                  />
                  {{ formatNumber(season.premium_price_ypoints) }}
                </span>
              </div>
              <div class="flex items-center justify-between text-xs">
                <span class="text-muted-foreground">
                  {{ $t("pages.battle_pass.your_balance") }}
                </span>
                <span class="tabular-nums">
                  {{ formatNumber(data.balance) }}
                </span>
              </div>
              <Button
                v-if="canAffordPremium"
                class="mt-auto gap-2 bg-violet-600 text-white hover:bg-violet-500"
                :disabled="buying"
                @click="buyPremium"
              >
                <Sparkles class="h-4 w-4" />
                {{ $t("pages.battle_pass.buy_premium") }}
              </Button>
              <Button v-else as-child variant="outline" class="mt-auto gap-2">
                <NuxtLink to="/store">
                  <ShoppingBag class="h-4 w-4" />
                  {{ $t("pages.battle_pass.get_ypoints") }}
                </NuxtLink>
              </Button>
            </template>
            <p v-else class="mt-auto text-sm text-violet-300">
              {{ $t("pages.battle_pass.premium_owned") }}
            </p>
          </div>
        </div>

        <section class="space-y-3">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <h2 class="text-lg font-semibold">
              {{ $t("pages.battle_pass.rewards_title") }}
            </h2>
            <Button
              v-if="claimableCount > 0"
              size="sm"
              class="gap-2"
              :disabled="claimingAll"
              @click="claimAll"
            >
              <Gift class="h-4 w-4" />
              {{ $t("pages.battle_pass.claim_all", { count: claimableCount }) }}
            </Button>
          </div>

          <div class="flex gap-3 rounded-xl border border-border/60 bg-muted/10 p-3">
            <div class="flex w-20 shrink-0 flex-col gap-2 pt-8">
              <div
                class="flex h-24 items-center text-xs font-semibold uppercase tracking-wider text-muted-foreground"
              >
                {{ $t("pages.battle_pass.free_track") }}
              </div>
              <div
                class="flex h-24 items-center gap-1 text-xs font-semibold uppercase tracking-wider text-violet-400"
              >
                <Crown class="h-3.5 w-3.5" />
                {{ $t("pages.battle_pass.premium_track") }}
              </div>
            </div>
            <div
              ref="track"
              class="flex gap-2 overflow-x-auto scroll-smooth pb-2"
            >
              <div
                v-for="row in data.levels"
                :key="row.level"
                :data-level="row.level"
                class="flex w-24 shrink-0 flex-col gap-2"
              >
                <div
                  class="flex h-6 items-center justify-center rounded text-xs font-bold tabular-nums"
                  :class="
                    row.level <= data.level
                      ? 'bg-[hsl(var(--tac-amber))] text-black'
                      : row.level === data.level + 1
                        ? 'border border-[hsl(var(--tac-amber))] text-[hsl(var(--tac-amber))]'
                        : 'bg-muted/40 text-muted-foreground'
                  "
                >
                  {{ row.level }}
                </div>
                <BattlePassRewardCell
                  :reward="row.free"
                  :reached="row.level <= data.level"
                  :claimed="row.free_claimed"
                  :locked="false"
                  :busy="busyCell === `${row.level}:free`"
                  @claim="claim(row.level, 'free')"
                />
                <BattlePassRewardCell
                  :reward="row.premium"
                  premium
                  :reached="row.level <= data.level"
                  :claimed="row.premium_claimed"
                  :locked="!data.premium"
                  :busy="busyCell === `${row.level}:premium`"
                  @claim="claim(row.level, 'premium')"
                />
              </div>
            </div>
          </div>
          <p class="text-xs text-muted-foreground">
            {{ $t("pages.battle_pass.xp_rules") }}
          </p>
        </section>

        <section class="grid gap-4 lg:grid-cols-2">
          <div
            v-for="group in [
              { id: 'daily', block: data.daily },
              { id: 'weekly', block: data.weekly },
            ]"
            :key="group.id"
            class="space-y-3 rounded-xl border border-border/60 p-5"
          >
            <div class="flex items-center justify-between gap-2">
              <h3 class="font-semibold">
                {{ $t(`pages.battle_pass.${group.id}_title`) }}
              </h3>
              <span
                class="inline-flex items-center gap-1 text-xs text-muted-foreground"
              >
                <CalendarClock class="h-3.5 w-3.5" />
                {{
                  $t("pages.battle_pass.resets", {
                    time: formatIn(group.block.resets_at),
                  })
                }}
              </span>
            </div>
            <div
              v-for="mission in group.block.missions"
              :key="mission.key"
              class="space-y-1.5 rounded-lg border p-3"
              :class="
                mission.completed
                  ? 'border-emerald-500/40 bg-emerald-500/5'
                  : 'border-border/50 bg-background/40'
              "
            >
              <div class="flex items-start justify-between gap-2 text-sm">
                <span class="font-medium">
                  {{ missionTitle(mission.key, mission.target) }}
                </span>
                <span
                  class="shrink-0 rounded bg-[hsl(var(--tac-amber)/0.15)] px-1.5 py-0.5 font-mono text-xs text-[hsl(var(--tac-amber))]"
                >
                  +{{ formatNumber(mission.xp) }} XP
                </span>
              </div>
              <div class="flex items-center gap-2">
                <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
                  <div
                    class="h-full rounded-full transition-all"
                    :class="
                      mission.completed
                        ? 'bg-emerald-500'
                        : 'bg-[hsl(var(--tac-amber))]'
                    "
                    :style="{ width: `${missionPct(mission)}%` }"
                  />
                </div>
                <span class="font-mono text-xs tabular-nums text-muted-foreground">
                  {{ mission.progress }}/{{ mission.target }}
                </span>
              </div>
            </div>
          </div>
        </section>

        <section
          v-if="data.subscriber"
          class="space-y-3 rounded-xl border border-amber-500/30 bg-amber-500/5 p-5"
        >
          <div class="flex flex-wrap items-center justify-between gap-2">
            <h3 class="flex items-center gap-2 font-semibold">
              <Swords class="h-4 w-4 text-amber-400" />
              {{ $t("pages.battle_pass.subscriber_title") }}
            </h3>
            <Badge v-if="data.subscriber.subscription" variant="secondary">
              {{
                data.subscriber.subscription.tier === "premium_plus"
                  ? $t("pages.challenges.tier_plus")
                  : $t("pages.challenges.tier_premium")
              }}
            </Badge>
          </div>

          <template v-if="data.subscriber.unlocked">
            <p class="text-xs text-muted-foreground">
              {{
                $t("pages.battle_pass.subscriber_help", {
                  xp: data.subscriber.xp_per_challenge,
                })
              }}
            </p>
            <div class="grid gap-3 md:grid-cols-2">
              <div
                v-for="challenge in data.subscriber.challenges"
                :key="challenge.id"
                class="space-y-1.5 rounded-lg border p-3"
                :class="
                  challenge.completed
                    ? 'border-emerald-500/40 bg-emerald-500/5'
                    : 'border-border/50 bg-background/40'
                "
              >
                <div class="flex items-start justify-between gap-2 text-sm">
                  <span class="font-medium">
                    {{ $t(`pages.challenges.catalog.${challenge.key}.title`) }}
                  </span>
                  <span class="flex shrink-0 items-center gap-1 text-xs">
                    <img
                      src="/img/ypoint-logo.png"
                      alt=""
                      class="h-3.5 w-3.5 object-contain"
                    />
                    +{{ challenge.reward_ypoints }}
                    <span class="font-mono text-[hsl(var(--tac-amber))]">
                      +{{ data.subscriber.xp_per_challenge }} XP
                    </span>
                  </span>
                </div>
                <p class="text-xs text-muted-foreground">
                  {{
                    $t(`pages.challenges.catalog.${challenge.key}.description`)
                  }}
                </p>
                <div class="flex items-center gap-2">
                  <div
                    class="h-1.5 flex-1 overflow-hidden rounded-full bg-muted"
                  >
                    <div
                      class="h-full rounded-full transition-all"
                      :class="
                        challenge.completed
                          ? 'bg-emerald-500'
                          : 'bg-[hsl(var(--tac-amber))]'
                      "
                      :style="{ width: `${missionPct(challenge)}%` }"
                    />
                  </div>
                  <span
                    class="font-mono text-xs tabular-nums text-muted-foreground"
                  >
                    {{ challenge.progress }}/{{ challenge.target }}
                  </span>
                </div>
              </div>
            </div>
          </template>

          <template v-else>
            <p class="text-sm text-muted-foreground">
              {{ $t("pages.battle_pass.subscriber_locked") }}
            </p>
            <div class="flex flex-wrap gap-2">
              <Button
                v-for="product in data.subscriber.products"
                :key="product.id"
                as-child
                size="sm"
                variant="outline"
                class="gap-2"
              >
                <NuxtLink to="/store">
                  <ShoppingBag class="h-3.5 w-3.5" />
                  {{ product.title }} · {{ formatPrice(product.price_irr) }}
                </NuxtLink>
              </Button>
            </div>
          </template>
        </section>
      </template>
    </div>
  </PageTransition>
</template>
