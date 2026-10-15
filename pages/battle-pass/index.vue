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
    bonus?: number;
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

/** Compact countdown like xplay’s top-left season timer. */
function countdownParts(iso: string) {
  const ms = Math.max(0, new Date(iso).getTime() - Date.now());
  const totalH = Math.floor(ms / 3_600_000);
  const d = Math.floor(totalH / 24);
  const h = totalH % 24;
  const m = Math.floor((ms % 3_600_000) / 60_000);
  return { d, h, m };
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
    <div
      class="relative left-1/2 w-screen max-w-[100vw] -translate-x-1/2 overflow-x-hidden bg-[#07090d] text-white"
    >
      <div
        class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,_rgba(244,63,94,0.12),_transparent_55%),radial-gradient(ellipse_at_bottom_right,_rgba(234,179,8,0.08),_transparent_45%)]"
        aria-hidden="true"
      />

      <div class="relative mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 pb-20 pt-5 md:px-6">
        <div v-if="!auth.me" class="py-16 text-center text-sm text-white/60">
          {{ $t("pages.battle_pass.login_required") }}
        </div>

        <div v-else-if="loading && !data" class="space-y-4">
          <Skeleton class="h-28 w-full bg-white/5" />
          <Skeleton class="h-72 w-full bg-white/5" />
        </div>

        <div
          v-else-if="data && !data.season"
          class="rounded-xl border border-white/10 bg-white/[0.03] p-8 text-sm text-white/60"
        >
          {{ $t("pages.battle_pass.no_season") }}
        </div>

        <template v-else-if="data && season">
          <!-- Top bar: timer + title + upgrade (xplay-style) -->
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div class="flex flex-wrap items-center gap-4">
              <div
                class="rounded-lg border border-white/10 bg-black/40 px-3 py-2 font-mono text-[11px] uppercase tracking-[0.18em] text-white/70"
              >
                <span class="text-white/40">{{ $t("pages.battle_pass.ends_in") }}</span>
                <span class="ms-2 tabular-nums text-rose-300">
                  {{ countdownParts(season.ends_at).d }}d
                  {{ countdownParts(season.ends_at).h }}h
                  {{ countdownParts(season.ends_at).m }}m
                </span>
              </div>
              <div>
                <h1
                  class="m-0 font-[Oxanium] text-2xl font-black uppercase tracking-[0.08em] md:text-3xl"
                >
                  {{ $t("pages.battle_pass.title") }}
                </h1>
                <p class="m-0 mt-0.5 text-sm text-white/50">
                  {{
                    $t("pages.battle_pass.season_name", {
                      season: season.number,
                    })
                  }}
                  ·
                  {{
                    $t("pages.battle_pass.total_xp", {
                      xp: formatNumber(data.xp),
                    })
                  }}
                </p>
              </div>
            </div>

            <div class="flex flex-wrap items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                class="gap-2 text-white/70 hover:bg-white/5 hover:text-white"
                :disabled="loading"
                @click="refresh"
              >
                <RefreshCw class="h-4 w-4" :class="{ 'animate-spin': loading }" />
                {{ $t("pages.battle_pass.refresh") }}
              </Button>
              <Badge
                v-if="data.premium"
                class="gap-1 border-0 bg-rose-500/90 px-3 py-1.5 text-white hover:bg-rose-500/90"
              >
                <Crown class="h-3.5 w-3.5" />
                {{ $t("pages.battle_pass.premium_active") }}
              </Badge>
              <template v-else>
                <Button
                  v-if="canAffordPremium"
                  class="gap-2 bg-rose-600 font-semibold text-white hover:bg-rose-500"
                  :disabled="buying"
                  @click="buyPremium"
                >
                  <Sparkles class="h-4 w-4" />
                  {{ $t("pages.battle_pass.buy_premium") }}
                  ·
                  <img src="/img/ypoint-logo.png" alt="" class="h-4 w-4 object-contain" />
                  {{ formatNumber(season.premium_price_ypoints) }}
                </Button>
                <Button v-else as-child variant="outline" class="gap-2 border-white/20 bg-white/5 text-white">
                  <NuxtLink to="/store">
                    <ShoppingBag class="h-4 w-4" />
                    {{ $t("pages.battle_pass.get_ypoints") }}
                  </NuxtLink>
                </Button>
              </template>
            </div>
          </div>

          <!-- Level + XP bar -->
          <div
            class="flex flex-wrap items-center gap-5 rounded-2xl border border-white/10 bg-gradient-to-r from-[#12151c] to-[#0c0e14] p-4 md:p-5"
          >
            <div
              class="flex h-[4.5rem] w-[4.5rem] flex-col items-center justify-center rounded-xl border border-rose-400/40 bg-gradient-to-b from-rose-500/20 to-transparent"
            >
              <span class="font-[Oxanium] text-[9px] uppercase tracking-[0.2em] text-rose-200/80">
                {{ $t("pages.battle_pass.level") }}
              </span>
              <span class="font-[Oxanium] text-3xl font-black tabular-nums leading-none">
                {{ data.level }}
              </span>
            </div>
            <div class="min-w-[12rem] flex-1 space-y-2">
              <div class="flex justify-between text-xs text-white/50">
                <span v-if="data.level < data.max_level">
                  {{
                    $t("pages.battle_pass.next_level", {
                      level: data.level + 1,
                    })
                  }}
                </span>
                <span v-else>{{ $t("pages.battle_pass.max_level") }}</span>
                <span class="font-mono tabular-nums">
                  {{ formatNumber(levelXp) }} /
                  {{ formatNumber(data.xp_per_level) }} XP
                </span>
              </div>
              <div class="h-2.5 overflow-hidden rounded-full bg-white/10">
                <div
                  class="h-full rounded-full bg-gradient-to-r from-rose-500 to-amber-400 transition-all"
                  :style="{ width: `${levelPct}%` }"
                />
              </div>
            </div>
            <Button
              v-if="claimableCount > 0"
              size="sm"
              class="gap-2 bg-emerald-600 text-white hover:bg-emerald-500"
              :disabled="claimingAll"
              @click="claimAll"
            >
              <Gift class="h-4 w-4" />
              {{ $t("pages.battle_pass.claim_all", { count: claimableCount }) }}
            </Button>
          </div>

          <!-- Reward track: Premium top / Free bottom (xplay) -->
          <section
            class="overflow-hidden rounded-2xl border border-white/10 bg-[#0a0c11] shadow-[0_20px_60px_rgba(0,0,0,0.45)]"
          >
            <div class="flex gap-0">
              <div
                class="hidden w-[5.5rem] shrink-0 flex-col border-e border-white/10 bg-black/30 sm:flex"
              >
                <div
                  class="flex h-[7.25rem] items-center justify-center gap-1 px-2 text-center text-[10px] font-bold uppercase tracking-[0.16em] text-rose-300"
                >
                  <Crown class="h-3.5 w-3.5" />
                  {{ $t("pages.battle_pass.premium_track") }}
                </div>
                <div
                  class="flex h-9 items-center justify-center border-y border-white/10 text-[10px] font-bold uppercase tracking-[0.16em] text-white/40"
                >
                  {{ $t("pages.battle_pass.level") }}
                </div>
                <div
                  class="flex h-[7.25rem] items-center justify-center px-2 text-center text-[10px] font-bold uppercase tracking-[0.16em] text-emerald-300/90"
                >
                  {{ $t("pages.battle_pass.free_track") }}
                </div>
              </div>

              <div
                ref="track"
                class="flex flex-1 gap-2 overflow-x-auto scroll-smooth px-3 py-3 md:gap-2.5 md:px-4"
              >
                <div
                  v-for="row in data.levels"
                  :key="row.level"
                  :data-level="row.level"
                  class="flex w-[6.75rem] shrink-0 flex-col gap-2"
                >
                  <BattlePassRewardCell
                    :reward="row.premium"
                    premium
                    :reached="row.level <= data.level"
                    :claimed="row.premium_claimed"
                    :locked="!data.premium"
                    :busy="busyCell === `${row.level}:premium`"
                    @claim="claim(row.level, 'premium')"
                  />
                  <div
                    class="flex h-9 items-center justify-center rounded-md font-[Oxanium] text-sm font-black tabular-nums"
                    :class="
                      row.level <= data.level
                        ? 'bg-rose-500 text-white'
                        : row.level === data.level + 1
                          ? 'border border-rose-400 text-rose-300'
                          : 'bg-white/5 text-white/35'
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
                </div>
              </div>
            </div>
            <p class="border-t border-white/10 px-4 py-2.5 text-xs text-white/40">
              {{ $t("pages.battle_pass.xp_rules") }}
            </p>
          </section>

          <!-- Challenges -->
          <section class="grid gap-4 lg:grid-cols-2">
            <div
              v-for="group in [
                { id: 'daily', block: data.daily },
                { id: 'weekly', block: data.weekly },
              ]"
              :key="group.id"
              class="space-y-3 rounded-2xl border border-white/10 bg-[#0c0e14] p-5"
            >
              <div class="flex items-center justify-between gap-2">
                <h3 class="m-0 font-[Oxanium] text-sm font-bold uppercase tracking-[0.12em]">
                  {{ $t(`pages.battle_pass.${group.id}_title`) }}
                </h3>
                <span class="inline-flex items-center gap-1 text-xs text-white/45">
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
                class="space-y-1.5 rounded-xl border p-3"
                :class="
                  mission.completed
                    ? 'border-emerald-500/40 bg-emerald-500/10'
                    : 'border-white/10 bg-black/25'
                "
              >
                <div class="flex items-start justify-between gap-2 text-sm">
                  <span class="font-medium text-white/90">
                    {{ missionTitle(mission.key, mission.target) }}
                  </span>
                  <span
                    class="shrink-0 rounded bg-amber-400/15 px-1.5 py-0.5 font-mono text-xs text-amber-300"
                  >
                    +{{ formatNumber(mission.xp) }} XP
                  </span>
                </div>
                <div class="flex items-center gap-2">
                  <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
                    <div
                      class="h-full rounded-full transition-all"
                      :class="mission.completed ? 'bg-emerald-500' : 'bg-rose-500'"
                      :style="{ width: `${missionPct(mission)}%` }"
                    />
                  </div>
                  <span class="font-mono text-xs tabular-nums text-white/45">
                    {{ mission.progress }}/{{ mission.target }}
                  </span>
                </div>
              </div>
            </div>
          </section>

          <section
            v-if="data.subscriber"
            class="space-y-3 rounded-2xl border border-amber-500/25 bg-amber-500/[0.06] p-5"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h3 class="m-0 flex items-center gap-2 font-[Oxanium] text-sm font-bold uppercase tracking-[0.12em]">
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
              <p class="m-0 text-xs text-white/50">
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
                  class="space-y-1.5 rounded-xl border p-3"
                  :class="
                    challenge.completed
                      ? 'border-emerald-500/40 bg-emerald-500/10'
                      : 'border-white/10 bg-black/25'
                  "
                >
                  <div class="flex items-start justify-between gap-2 text-sm">
                    <span class="font-medium">
                      {{ $t(`pages.challenges.catalog.${challenge.key}.title`) }}
                    </span>
                    <span class="flex shrink-0 items-center gap-1 text-xs">
                      <img src="/img/ypoint-logo.png" alt="" class="h-3.5 w-3.5 object-contain" />
                      +{{ challenge.reward_ypoints }}
                      <span class="font-mono text-amber-300">
                        +{{ data.subscriber.xp_per_challenge }} XP
                      </span>
                    </span>
                  </div>
                  <p class="m-0 text-xs text-white/45">
                    {{ $t(`pages.challenges.catalog.${challenge.key}.description`) }}
                  </p>
                  <div class="flex items-center gap-2">
                    <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
                      <div
                        class="h-full rounded-full transition-all"
                        :class="challenge.completed ? 'bg-emerald-500' : 'bg-amber-400'"
                        :style="{ width: `${missionPct(challenge)}%` }"
                      />
                    </div>
                    <span class="font-mono text-xs tabular-nums text-white/45">
                      {{ challenge.progress }}/{{ challenge.target }}
                    </span>
                  </div>
                </div>
              </div>
            </template>

            <template v-else>
              <p class="m-0 text-sm text-white/55">
                {{ $t("pages.battle_pass.subscriber_locked") }}
              </p>
              <div class="flex flex-wrap gap-2">
                <Button
                  v-for="product in data.subscriber.products"
                  :key="product.id"
                  as-child
                  size="sm"
                  variant="outline"
                  class="gap-2 border-white/20 bg-white/5 text-white"
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
    </div>
  </PageTransition>
</template>
