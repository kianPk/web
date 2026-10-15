<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  Award,
  Check,
  ChevronLeft,
  ChevronRight,
  Crown,
  Gift,
  HelpCircle,
  Lock,
  Sparkles,
  Zap,
} from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import type { BattlePassReward } from "~/components/battle-pass/types";
import { Button } from "~/components/ui/button";
import { Skeleton } from "~/components/ui/skeleton";
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

type LevelRow = {
  level: number;
  free: BattlePassReward | null;
  premium: BattlePassReward | null;
  free_claimed: boolean;
  premium_claimed: boolean;
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
  levels: LevelRow[];
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

type TrackItem = {
  key: string;
  level: number;
  track: "free" | "premium";
  reward: BattlePassReward;
  claimed: boolean;
  locked: boolean;
  reached: boolean;
};

const PAGE_SIZE = 9;

const { t, locale } = useI18n();
const auth = useAuthStore();
const { refresh: refreshYpoints } = useYpoints();
const apiDomain = useRuntimeConfig().public.apiDomain as string;

const loading = ref(true);
const data = ref<BattlePassResponse | null>(null);
const busyCell = ref<string | null>(null);
const claimingAll = ref(false);
const buying = ref(false);
const page = ref(0);
const selectedKey = ref<string | null>(null);
const xpHelpOpen = ref(false);

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

const challengeCount = computed(() => {
  if (!data.value) return 0;
  return (
    data.value.daily.missions.length +
    data.value.weekly.missions.length +
    (data.value.subscriber?.challenges.length || 0)
  );
});

const trackItems = computed<TrackItem[]>(() => {
  if (!data.value) return [];
  const items: TrackItem[] = [];
  for (const row of data.value.levels) {
    if (row.free) {
      items.push({
        key: `${row.level}:free`,
        level: row.level,
        track: "free",
        reward: row.free,
        claimed: row.free_claimed,
        locked: false,
        reached: row.level <= data.value.level,
      });
    }
    if (row.premium) {
      items.push({
        key: `${row.level}:premium`,
        level: row.level,
        track: "premium",
        reward: row.premium,
        claimed: row.premium_claimed,
        locked: !data.value.premium,
        reached: row.level <= data.value.level,
      });
    }
  }
  return items;
});

const pageCount = computed(() =>
  Math.max(1, Math.ceil(trackItems.value.length / PAGE_SIZE)),
);

const pageItems = computed(() => {
  const start = page.value * PAGE_SIZE;
  return trackItems.value.slice(start, start + PAGE_SIZE);
});

const selected = computed(
  () =>
    trackItems.value.find((item) => item.key === selectedKey.value) ||
    pageItems.value[0] ||
    null,
);

const pageNumbers = computed(() => {
  const total = pageCount.value;
  const current = page.value;
  const nums: number[] = [];
  for (let i = 0; i < total; i++) {
    if (i < 6 || i === total - 1 || Math.abs(i - current) <= 1) {
      nums.push(i);
    }
  }
  return nums;
});

watch(trackItems, (items) => {
  if (!items.length) {
    selectedKey.value = null;
    return;
  }
  if (!selectedKey.value || !items.some((i) => i.key === selectedKey.value)) {
    const claimable = items.find(
      (i) => i.reached && !i.claimed && !i.locked,
    );
    const current = items.find((i) => i.level === (data.value?.level || 1));
    selectedKey.value = (claimable || current || items[0]).key;
    const idx = items.findIndex((i) => i.key === selectedKey.value);
    if (idx >= 0) page.value = Math.floor(idx / PAGE_SIZE);
  }
});

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

function rewardLabel(reward: BattlePassReward) {
  if (reward.type === "ypoints") return `${reward.amount} YP`;
  if (reward.type === "xp") return `+${reward.amount} XP`;
  if (reward.type === "subscription") {
    return t("pages.battle_pass.reward.subscription", {
      tier:
        reward.tier === "premium_plus"
          ? t("pages.challenges.tier_plus")
          : t("pages.challenges.tier_premium"),
      days: reward.days,
    });
  }
  return t(`pages.battle_pass.badge.${reward.badge}`);
}

function selectItem(item: TrackItem) {
  selectedKey.value = item.key;
}

function goPage(n: number) {
  page.value = Math.max(0, Math.min(pageCount.value - 1, n));
}

onMounted(() => {
  void refresh();
});
</script>

<template>
  <PageTransition>
    <div class="mx-auto w-full max-w-[1400px] px-3 pb-16 pt-4 md:px-5">
      <div v-if="!auth.me" class="py-20 text-center text-sm text-muted-foreground">
        {{ $t("pages.battle_pass.login_required") }}
      </div>

      <div v-else-if="loading && !data" class="grid gap-4 lg:grid-cols-[320px_1fr]">
        <Skeleton class="h-[36rem] rounded-xl" />
        <Skeleton class="h-[36rem] rounded-xl" />
      </div>

      <div
        v-else-if="data && !data.season"
        class="rounded-xl border border-border/60 bg-muted/20 p-8 text-sm text-muted-foreground"
      >
        {{ $t("pages.battle_pass.no_season") }}
      </div>

      <div
        v-else-if="data && season"
        class="grid gap-3 lg:grid-cols-[300px_minmax(0,1fr)] xl:grid-cols-[320px_minmax(0,1fr)]"
      >
        <!-- LEFT: season + challenges + upgrade -->
        <aside class="flex flex-col gap-3">
          <div
            class="overflow-hidden rounded-xl border border-white/10 bg-[#12151c]"
          >
            <div
              class="relative px-4 pb-3 pt-4"
              style="
                background:
                  radial-gradient(ellipse 90% 80% at 20% 0%, hsla(32, 90%, 50%, 0.35), transparent 55%),
                  linear-gradient(160deg, #2a1a0c 0%, #12151c 70%);
              "
            >
              <div class="text-[1.35rem] font-black uppercase tracking-wide text-white">
                {{ $t("pages.battle_pass.title") }}
                {{ season.number }}
              </div>
              <div class="mt-2 flex flex-wrap gap-1.5">
                <span
                  class="rounded bg-black/40 px-2 py-0.5 text-[11px] font-semibold text-white/80"
                >
                  {{ $t("pages.battle_pass.levels_badge", { n: data.max_level }) }}
                </span>
                <span
                  class="rounded bg-black/40 px-2 py-0.5 text-[11px] font-semibold text-white/80"
                >
                  {{
                    $t("pages.battle_pass.challenges_badge", {
                      n: challengeCount,
                    })
                  }}
                </span>
              </div>
              <div class="mt-3 font-mono text-xs text-white/55">
                {{ $t("pages.battle_pass.season_end") }}
                <span class="ms-1 text-[hsl(var(--tac-amber))]">
                  {{ countdownParts(season.ends_at).d }}d,
                  {{ countdownParts(season.ends_at).h }}h,
                  {{ countdownParts(season.ends_at).m }}min
                </span>
              </div>
            </div>

            <div class="space-y-2 border-t border-white/10 px-3 py-3">
              <div class="flex items-center justify-between gap-2 text-xs text-white/50">
                <span>
                  {{ $t("pages.battle_pass.level") }}
                  {{ data.level }}
                </span>
                <span class="tabular-nums">
                  {{ formatNumber(levelXp) }} /
                  {{ formatNumber(data.xp_per_level) }} XP
                </span>
              </div>
              <div class="h-1.5 overflow-hidden rounded-full bg-white/10">
                <div
                  class="h-full rounded-full bg-[hsl(var(--tac-amber))]"
                  :style="{ width: `${levelPct}%` }"
                />
              </div>
              <button
                type="button"
                class="inline-flex items-center gap-1 text-xs text-sky-400 hover:text-sky-300"
                @click="xpHelpOpen = !xpHelpOpen"
              >
                <HelpCircle class="h-3.5 w-3.5" />
                {{ $t("pages.battle_pass.xp_how") }}
              </button>
              <p
                v-if="xpHelpOpen"
                class="m-0 rounded-lg bg-black/30 p-2 text-[11px] leading-relaxed text-white/55"
              >
                {{ $t("pages.battle_pass.xp_rules") }}
              </p>
            </div>
          </div>

          <div
            v-for="group in [
              { id: 'daily', block: data.daily },
              { id: 'weekly', block: data.weekly },
            ]"
            :key="group.id"
            class="rounded-xl border border-white/10 bg-[#12151c] p-3"
          >
            <div class="mb-2 flex items-center justify-between gap-2">
              <h3 class="m-0 text-sm font-bold text-white">
                {{ $t(`pages.battle_pass.${group.id}_title`) }}
              </h3>
            </div>
            <p class="m-0 mb-2 text-[11px] text-white/40">
              {{
                $t("pages.battle_pass.resets", {
                  time: formatIn(group.block.resets_at),
                })
              }}
            </p>
            <div class="space-y-2">
              <div
                v-for="mission in group.block.missions"
                :key="mission.key"
                class="rounded-lg border border-white/[0.07] bg-[#0c0f14] p-2.5"
              >
                <div class="flex items-start justify-between gap-2">
                  <span class="text-[12px] font-medium leading-snug text-white/90">
                    {{ missionTitle(mission.key, mission.target) }}
                  </span>
                  <span
                    class="shrink-0 font-mono text-[11px] tabular-nums text-white/45"
                  >
                    {{ mission.progress }} / {{ mission.target }}
                  </span>
                </div>
                <div class="mt-2 flex flex-wrap items-center gap-1.5">
                  <span
                    class="rounded bg-[hsl(var(--tac-amber)/0.18)] px-1.5 py-0.5 font-mono text-[10px] font-semibold text-[hsl(var(--tac-amber))]"
                  >
                    {{ formatNumber(mission.xp) }} XP
                  </span>
                  <span
                    v-if="data.premium"
                    class="rounded bg-orange-500/20 px-1.5 py-0.5 text-[10px] font-semibold text-orange-300"
                  >
                    BP +{{ Math.round(mission.xp * 0.25) }}XP
                  </span>
                </div>
                <div class="mt-2 h-1 overflow-hidden rounded-full bg-white/10">
                  <div
                    class="h-full rounded-full"
                    :class="
                      mission.completed
                        ? 'bg-emerald-500'
                        : 'bg-[hsl(var(--tac-amber))]'
                    "
                    :style="{ width: `${missionPct(mission)}%` }"
                  />
                </div>
              </div>
            </div>
          </div>

          <div
            class="mt-auto overflow-hidden rounded-xl border border-orange-500/35 bg-gradient-to-b from-[#2a1608] to-[#12151c] p-4"
          >
            <template v-if="!data.premium">
              <div class="text-xs font-semibold uppercase tracking-wider text-orange-300">
                {{ $t("pages.battle_pass.upgrade_bp") }}
              </div>
              <div class="mt-1 flex items-end gap-2">
                <span class="text-2xl font-black text-white">
                  {{ formatNumber(season.premium_price_ypoints) }}
                </span>
                <img src="/img/ypoint-logo.png" alt="" class="mb-1 h-5 w-5" />
              </div>
              <p class="m-0 mt-1 text-[11px] text-white/50">
                {{ $t("pages.battle_pass.upgrade_benefit") }}
              </p>
              <Button
                v-if="canAffordPremium"
                class="mt-3 w-full bg-orange-500 font-bold text-black hover:bg-orange-400"
                :disabled="buying"
                @click="buyPremium"
              >
                <Sparkles class="h-4 w-4" />
                {{ $t("pages.battle_pass.upgrade_bp") }}
              </Button>
              <Button
                v-else
                as-child
                class="mt-3 w-full bg-orange-500 font-bold text-black hover:bg-orange-400"
              >
                <NuxtLink to="/store">
                  {{ $t("pages.battle_pass.get_ypoints") }}
                </NuxtLink>
              </Button>
            </template>
            <template v-else>
              <div class="flex items-center gap-2 text-sm font-semibold text-orange-300">
                <Crown class="h-4 w-4" />
                {{ $t("pages.battle_pass.premium_active") }}
              </div>
              <p class="m-0 mt-1 text-[11px] text-white/50">
                {{ $t("pages.battle_pass.premium_owned") }}
              </p>
            </template>
          </div>
        </aside>

        <!-- RIGHT: preview + track -->
        <section class="flex min-w-0 flex-col gap-3">
          <div class="flex flex-wrap items-center gap-2">
            <Button
              v-if="claimableCount > 0"
              size="sm"
              class="gap-1.5 bg-emerald-600 text-white hover:bg-emerald-500"
              :disabled="claimingAll"
              @click="claimAll"
            >
              <Gift class="h-3.5 w-3.5" />
              {{ $t("pages.battle_pass.claim_all", { count: claimableCount }) }}
            </Button>
            <div
              class="ms-auto rounded-lg border border-white/10 bg-[#12151c] px-3 py-1.5 font-mono text-xs text-white/60"
            >
              {{ formatNumber(data.balance) }} YP
            </div>
          </div>

          <!-- Big reward preview -->
          <div
            class="relative flex min-h-[280px] flex-1 flex-col items-center justify-center overflow-hidden rounded-xl border border-white/10 bg-[#0e1117] px-4 py-10"
          >
            <div
              class="pointer-events-none absolute inset-0 opacity-40"
              style="
                background: radial-gradient(
                  ellipse 50% 50% at 50% 45%,
                  hsla(42, 90%, 50%, 0.18),
                  transparent 65%
                );
              "
              aria-hidden="true"
            />
            <template v-if="selected">
              <div class="relative flex flex-col items-center text-center">
                <template v-if="selected.reward.type === 'ypoints'">
                  <img
                    src="/img/ypoint-logo.png"
                    alt=""
                    class="h-28 w-28 object-contain drop-shadow-[0_8px_28px_rgba(245,165,36,0.45)]"
                  />
                  <div class="mt-3 text-4xl font-black tabular-nums text-white">
                    + {{ selected.reward.amount }}
                  </div>
                </template>
                <template v-else-if="selected.reward.type === 'xp'">
                  <Zap class="h-28 w-28 text-sky-400 drop-shadow-[0_8px_28px_rgba(56,189,248,0.45)]" />
                  <div class="mt-3 text-4xl font-black tabular-nums text-sky-300">
                    + {{ selected.reward.amount }} XP
                  </div>
                </template>
                <template v-else-if="selected.reward.type === 'subscription'">
                  <Crown class="h-28 w-28 text-[hsl(var(--tac-amber))]" />
                  <div class="mt-3 text-2xl font-black text-white">
                    {{ rewardLabel(selected.reward) }}
                  </div>
                </template>
                <template v-else>
                  <Award class="h-28 w-28 text-[hsl(var(--tac-amber))]" />
                  <div class="mt-3 text-2xl font-black text-white">
                    {{ rewardLabel(selected.reward) }}
                  </div>
                </template>

                <p class="m-0 mt-3 text-sm text-white/50">
                  {{ $t("pages.battle_pass.level") }}
                  {{ selected.level }}
                  ·
                  {{
                    selected.track === "premium"
                      ? $t("pages.battle_pass.premium_track")
                      : $t("pages.battle_pass.free_track")
                  }}
                </p>

                <Button
                  v-if="
                    selected.reached && !selected.claimed && !selected.locked
                  "
                  class="mt-5 min-w-[10rem] bg-[hsl(var(--tac-amber))] font-bold text-black hover:brightness-110"
                  :disabled="busyCell === selected.key"
                  @click="claim(selected.level, selected.track)"
                >
                  {{ $t("pages.battle_pass.claim") }}
                </Button>
                <div
                  v-else-if="selected.claimed"
                  class="mt-5 inline-flex items-center gap-1.5 rounded-full bg-emerald-500/20 px-3 py-1.5 text-sm font-semibold text-emerald-300"
                >
                  <Check class="h-4 w-4" />
                  {{ $t("pages.battle_pass.claimed_toast") }}
                </div>
                <div
                  v-else
                  class="mt-5 inline-flex items-center gap-1.5 text-sm text-white/40"
                >
                  <Lock class="h-4 w-4" />
                  {{ $t("pages.battle_pass.reward_locked") }}
                </div>
              </div>
            </template>
          </div>

          <!-- Reward track -->
          <div class="rounded-xl border border-white/10 bg-[#12151c] p-3">
            <div class="flex gap-2 overflow-x-auto pb-1">
              <button
                v-for="item in pageItems"
                :key="item.key"
                type="button"
                class="relative flex w-[5.75rem] shrink-0 flex-col overflow-hidden rounded-lg border bg-[#0c0f14] transition"
                :class="
                  selectedKey === item.key
                    ? 'border-teal-400 shadow-[0_0_0_1px_rgba(45,212,191,0.5)]'
                    : 'border-white/10 hover:border-white/25'
                "
                @click="selectItem(item)"
              >
                <div
                  class="flex h-7 items-center justify-center border-b border-white/10 text-xs font-bold tabular-nums text-white/70"
                >
                  {{ item.level }}
                </div>
                <div
                  class="relative flex h-[4.5rem] flex-col items-center justify-center gap-0.5 px-1"
                >
                  <Lock
                    v-if="item.locked || !item.reached"
                    class="absolute right-1 top-1 h-3 w-3 text-white/30"
                  />
                  <Check
                    v-else-if="item.claimed"
                    class="absolute right-1 top-1 h-3 w-3 text-emerald-400"
                  />
                  <img
                    v-if="item.reward.type === 'ypoints'"
                    src="/img/ypoint-logo.png"
                    alt=""
                    class="h-7 w-7 object-contain"
                  />
                  <Zap
                    v-else-if="item.reward.type === 'xp'"
                    class="h-7 w-7 text-sky-400"
                  />
                  <Crown
                    v-else-if="item.reward.type === 'subscription'"
                    class="h-7 w-7 text-[hsl(var(--tac-amber))]"
                  />
                  <Award v-else class="h-7 w-7 text-[hsl(var(--tac-amber))]" />
                  <span
                    class="max-w-full truncate text-[11px] font-bold tabular-nums text-white"
                  >
                    <template v-if="item.reward.type === 'ypoints'">{{
                      item.reward.amount
                    }}</template>
                    <template v-else-if="item.reward.type === 'xp'"
                      >+{{ item.reward.amount }}</template
                    >
                    <template v-else>★</template>
                  </span>
                </div>
                <div
                  class="flex h-6 items-center justify-center text-[10px] font-bold uppercase tracking-wide"
                  :class="
                    item.track === 'premium'
                      ? 'bg-orange-500/20 text-orange-300'
                      : 'bg-white/5 text-white/45'
                  "
                >
                  {{
                    item.track === "premium"
                      ? $t("pages.battle_pass.premium_track")
                      : $t("pages.battle_pass.free_track")
                  }}
                </div>
              </button>
            </div>

            <div class="mt-3 flex items-center justify-center gap-1.5">
              <button
                type="button"
                class="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-white/50 hover:bg-white/5 hover:text-white disabled:opacity-30"
                :disabled="page <= 0"
                @click="goPage(page - 1)"
              >
                <ChevronLeft class="h-3.5 w-3.5" />
                {{ $t("pages.battle_pass.previous") }}
              </button>
              <template v-for="(n, idx) in pageNumbers" :key="n">
                <span
                  v-if="idx > 0 && pageNumbers[idx - 1] < n - 1"
                  class="px-1 text-white/30"
                  >…</span
                >
                <button
                  type="button"
                  class="min-w-[1.75rem] rounded px-1.5 py-1 text-xs font-semibold tabular-nums"
                  :class="
                    page === n
                      ? 'bg-white/15 text-white'
                      : 'text-white/45 hover:bg-white/5 hover:text-white'
                  "
                  @click="goPage(n)"
                >
                  {{ n + 1 }}
                </button>
              </template>
              <button
                type="button"
                class="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-white/50 hover:bg-white/5 hover:text-white disabled:opacity-30"
                :disabled="page >= pageCount - 1"
                @click="goPage(page + 1)"
              >
                {{ $t("pages.battle_pass.next") }}
                <ChevronRight class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>
        </section>
      </div>
    </div>
  </PageTransition>
</template>
