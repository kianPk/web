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
    <div class="relative mx-auto w-full max-w-6xl space-y-6 px-4 pb-16 pt-6">
      <div
        class="pointer-events-none absolute inset-x-0 -top-8 h-72 bg-[radial-gradient(ellipse_at_top,hsla(var(--tac-amber)/0.12),transparent_60%)]"
        aria-hidden="true"
      />

      <div v-if="!auth.me" class="relative py-20 text-center text-sm text-muted-foreground">
        {{ $t("pages.battle_pass.login_required") }}
      </div>

      <div v-else-if="loading && !data" class="relative space-y-4">
        <Skeleton class="h-40 w-full rounded-3xl" />
        <Skeleton class="h-72 w-full rounded-3xl" />
      </div>

      <div
        v-else-if="data && !data.season"
        class="relative rounded-3xl border border-border/60 bg-muted/20 p-8 text-sm text-muted-foreground"
      >
        {{ $t("pages.battle_pass.no_season") }}
      </div>

      <template v-else-if="data && season">
        <header
          class="relative overflow-hidden rounded-3xl border border-[hsl(var(--tac-amber)/0.28)] bg-[#080a0f] shadow-[0_20px_60px_rgba(0,0,0,0.35)]"
        >
          <div
            class="pointer-events-none absolute inset-0"
            style="
              background:
                radial-gradient(ellipse 70% 80% at 0% 0%, hsla(var(--tac-amber) / 0.22), transparent 50%),
                radial-gradient(ellipse 50% 60% at 100% 20%, rgba(56, 120, 255, 0.12), transparent 45%),
                linear-gradient(165deg, #141a26 0%, #080a0f 55%, #0c1018 100%);
            "
            aria-hidden="true"
          />
          <div
            class="pointer-events-none absolute inset-0 opacity-[0.07]"
            style="
              background-image: linear-gradient(hsla(0,0%,100%,0.08) 1px, transparent 1px),
                linear-gradient(90deg, hsla(0,0%,100%,0.08) 1px, transparent 1px);
              background-size: 28px 28px;
            "
            aria-hidden="true"
          />

          <div class="relative flex flex-wrap items-center justify-between gap-6 p-6 md:p-8">
            <div class="flex items-center gap-5">
              <div class="relative">
                <div
                  class="absolute -inset-1 rounded-[1.35rem] bg-[hsl(var(--tac-amber)/0.35)] blur-md"
                  aria-hidden="true"
                />
                <div
                  class="relative flex h-[5.25rem] w-[5.25rem] flex-col items-center justify-center rounded-[1.25rem] border border-[hsl(var(--tac-amber)/0.55)] bg-black/50"
                >
                  <span
                    class="text-[9px] font-bold uppercase tracking-[0.22em] text-[hsl(var(--tac-amber)/0.9)]"
                  >
                    {{ $t("pages.battle_pass.level") }}
                  </span>
                  <span class="text-4xl font-black tabular-nums leading-none text-white">{{
                    data.level
                  }}</span>
                </div>
              </div>
              <div>
                <div class="flex flex-wrap items-center gap-2">
                  <h1 class="m-0 text-2xl font-black tracking-tight text-white md:text-3xl">
                    {{ $t("pages.battle_pass.title") }}
                  </h1>
                  <Badge
                    v-if="data.premium"
                    class="border-0 bg-[hsl(var(--tac-amber))] text-black hover:bg-[hsl(var(--tac-amber))]"
                  >
                    <Crown class="me-1 h-3 w-3" />
                    {{ $t("pages.battle_pass.premium_active") }}
                  </Badge>
                </div>
                <p class="m-0 mt-1.5 text-sm text-white/55">
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
                <div
                  class="mt-3 inline-flex items-center gap-2 rounded-full border border-white/10 bg-black/30 px-3 py-1 font-mono text-[11px] uppercase tracking-[0.14em] text-white/45"
                >
                  <CalendarClock class="h-3.5 w-3.5 text-[hsl(var(--tac-amber))]" />
                  {{ $t("pages.battle_pass.ends_in") }}
                  <span class="text-[hsl(var(--tac-amber))]">
                    {{ countdownParts(season.ends_at).d }}d
                    {{ countdownParts(season.ends_at).h }}h
                    {{ countdownParts(season.ends_at).m }}m
                  </span>
                </div>
              </div>
            </div>

            <div class="flex w-full max-w-md flex-col gap-3 sm:w-auto">
              <div class="space-y-2 rounded-2xl border border-white/10 bg-black/25 p-3.5">
                <div class="flex justify-between text-xs text-white/50">
                  <span v-if="data.level < data.max_level">
                    {{
                      $t("pages.battle_pass.next_level", {
                        level: data.level + 1,
                      })
                    }}
                  </span>
                  <span v-else>{{ $t("pages.battle_pass.max_level") }}</span>
                  <span class="tabular-nums text-white/70">
                    {{ formatNumber(levelXp) }}/{{ formatNumber(data.xp_per_level) }}
                  </span>
                </div>
                <div class="h-2.5 overflow-hidden rounded-full bg-white/10">
                  <div
                    class="h-full rounded-full bg-gradient-to-r from-[hsl(var(--tac-amber)/0.7)] to-[hsl(var(--tac-amber))] transition-all duration-500"
                    :style="{ width: `${levelPct}%` }"
                  />
                </div>
              </div>
              <div class="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  class="border-white/15 bg-white/5 text-white hover:bg-white/10"
                  :disabled="loading"
                  @click="refresh"
                >
                  <RefreshCw class="h-3.5 w-3.5" :class="{ 'animate-spin': loading }" />
                </Button>
                <Button
                  v-if="claimableCount > 0"
                  size="sm"
                  class="gap-1.5 bg-emerald-600 text-white shadow-[0_0_20px_rgba(16,185,129,0.35)] hover:bg-emerald-500"
                  :disabled="claimingAll"
                  @click="claimAll"
                >
                  <Gift class="h-3.5 w-3.5" />
                  {{ $t("pages.battle_pass.claim_all", { count: claimableCount }) }}
                </Button>
                <template v-if="!data.premium">
                  <Button
                    v-if="canAffordPremium"
                    size="sm"
                    class="gap-1.5 bg-[hsl(var(--tac-amber))] font-semibold text-black shadow-[0_0_24px_hsla(var(--tac-amber)/0.4)] hover:brightness-110"
                    :disabled="buying"
                    @click="buyPremium"
                  >
                    <Sparkles class="h-3.5 w-3.5" />
                    {{ $t("pages.battle_pass.buy_premium") }}
                    · {{ formatNumber(season.premium_price_ypoints) }}
                  </Button>
                  <Button
                    v-else
                    as-child
                    size="sm"
                    variant="outline"
                    class="border-white/15 bg-white/5 text-white"
                  >
                    <NuxtLink to="/store">
                      <ShoppingBag class="h-3.5 w-3.5" />
                      {{ $t("pages.battle_pass.get_ypoints") }}
                    </NuxtLink>
                  </Button>
                </template>
              </div>
            </div>
          </div>
        </header>

        <section
          class="relative overflow-hidden rounded-3xl border border-white/10 bg-[#080a0f] shadow-[0_16px_48px_rgba(0,0,0,0.28)]"
        >
          <div class="flex items-center justify-between gap-3 border-b border-white/10 px-4 py-3.5 md:px-5">
            <h2 class="m-0 text-sm font-bold tracking-wide text-white/85">
              {{ $t("pages.battle_pass.rewards_title") }}
            </h2>
            <p class="m-0 hidden max-w-md text-end text-xs text-white/35 sm:block">
              {{ $t("pages.battle_pass.xp_rules") }}
            </p>
          </div>
          <div class="flex">
            <div class="hidden w-[5.5rem] shrink-0 flex-col border-e border-white/10 sm:flex">
              <div
                class="flex h-[7.25rem] flex-col items-center justify-center gap-1 px-2 text-center text-[10px] font-bold uppercase tracking-[0.14em] text-[hsl(var(--tac-amber))]"
              >
                <Crown class="h-4 w-4" />
                {{ $t("pages.battle_pass.premium_track") }}
              </div>
              <div
                class="flex h-9 items-center justify-center border-y border-white/10 text-[10px] font-semibold uppercase tracking-[0.14em] text-white/35"
              >
                {{ $t("pages.battle_pass.level") }}
              </div>
              <div
                class="flex h-[7.25rem] items-center justify-center px-2 text-center text-[10px] font-bold uppercase tracking-[0.14em] text-white/55"
              >
                {{ $t("pages.battle_pass.free_track") }}
              </div>
            </div>
            <div
              ref="track"
              class="flex flex-1 gap-2.5 overflow-x-auto px-3 py-4 scroll-smooth md:gap-3 md:px-4"
            >
              <div
                v-for="row in data.levels"
                :key="row.level"
                :data-level="row.level"
                class="flex w-[7.25rem] shrink-0 flex-col gap-2.5"
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
                  class="relative flex h-9 items-center justify-center rounded-lg text-xs font-black tabular-nums"
                  :class="
                    row.level <= data.level
                      ? 'bg-[hsl(var(--tac-amber))] text-black shadow-[0_0_18px_hsla(var(--tac-amber)/0.45)]'
                      : row.level === data.level + 1
                        ? 'border border-[hsl(var(--tac-amber))] text-[hsl(var(--tac-amber))]'
                        : 'bg-white/[0.04] text-white/30'
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
        </section>

        <section class="grid gap-4 lg:grid-cols-2">
          <div
            v-for="group in [
              { id: 'daily', block: data.daily },
              { id: 'weekly', block: data.weekly },
            ]"
            :key="group.id"
            class="rounded-3xl border border-border/50 bg-card/50 p-5 backdrop-blur-sm"
          >
            <div class="mb-4 flex items-center justify-between gap-2">
              <h3 class="m-0 text-base font-semibold">
                {{ $t(`pages.battle_pass.${group.id}_title`) }}
              </h3>
              <span
                class="inline-flex items-center gap-1 rounded-full border border-border/60 bg-muted/30 px-2.5 py-1 text-xs text-muted-foreground"
              >
                <CalendarClock class="h-3.5 w-3.5" />
                {{
                  $t("pages.battle_pass.resets", {
                    time: formatIn(group.block.resets_at),
                  })
                }}
              </span>
            </div>
            <div class="space-y-2.5">
              <div
                v-for="mission in group.block.missions"
                :key="mission.key"
                class="rounded-2xl border p-3.5 transition-colors"
                :class="
                  mission.completed
                    ? 'border-emerald-500/40 bg-emerald-500/[0.08]'
                    : 'border-border/50 bg-background/40'
                "
              >
                <div class="flex items-start justify-between gap-2 text-sm">
                  <span class="font-medium leading-snug">
                    {{ missionTitle(mission.key, mission.target) }}
                  </span>
                  <span
                    class="shrink-0 rounded-md bg-[hsl(var(--tac-amber)/0.14)] px-1.5 py-0.5 font-mono text-[11px] font-semibold text-[hsl(var(--tac-amber))]"
                  >
                    +{{ formatNumber(mission.xp) }} XP
                  </span>
                </div>
                <div class="mt-2.5 flex items-center gap-2">
                  <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
                    <div
                      class="h-full rounded-full transition-all duration-500"
                      :class="
                        mission.completed
                          ? 'bg-emerald-500'
                          : 'bg-[hsl(var(--tac-amber))]'
                      "
                      :style="{ width: `${missionPct(mission)}%` }"
                    />
                  </div>
                  <span class="font-mono text-[11px] tabular-nums text-muted-foreground">
                    {{ mission.progress }}/{{ mission.target }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section
          v-if="data.subscriber"
          class="rounded-3xl border border-[hsl(var(--tac-amber)/0.3)] bg-gradient-to-br from-[hsl(var(--tac-amber)/0.08)] to-transparent p-5 md:p-6"
        >
          <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
            <h3 class="m-0 flex items-center gap-2 text-base font-semibold">
              <Swords class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
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
            <p class="m-0 mb-4 text-xs text-muted-foreground">
              {{
                $t("pages.battle_pass.subscriber_help", {
                  xp: data.subscriber.xp_per_challenge,
                })
              }}
            </p>
            <div class="grid gap-2.5 md:grid-cols-2">
              <div
                v-for="challenge in data.subscriber.challenges"
                :key="challenge.id"
                class="rounded-2xl border p-3.5"
                :class="
                  challenge.completed
                    ? 'border-emerald-500/40 bg-emerald-500/[0.08]'
                    : 'border-border/50 bg-background/40'
                "
              >
                <div class="flex items-start justify-between gap-2 text-sm">
                  <span class="font-medium">
                    {{ $t(`pages.challenges.catalog.${challenge.key}.title`) }}
                  </span>
                  <span class="flex shrink-0 items-center gap-1 text-xs">
                    <img src="/img/ypoint-logo.png" alt="" class="h-3.5 w-3.5 object-contain" />
                    +{{ challenge.reward_ypoints }}
                    <span class="font-mono text-[hsl(var(--tac-amber))]">
                      +{{ data.subscriber.xp_per_challenge }} XP
                    </span>
                  </span>
                </div>
                <p class="m-0 mt-1 text-xs text-muted-foreground">
                  {{ $t(`pages.challenges.catalog.${challenge.key}.description`) }}
                </p>
                <div class="mt-2.5 flex items-center gap-2">
                  <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
                    <div
                      class="h-full rounded-full transition-all duration-500"
                      :class="
                        challenge.completed
                          ? 'bg-emerald-500'
                          : 'bg-[hsl(var(--tac-amber))]'
                      "
                      :style="{ width: `${missionPct(challenge)}%` }"
                    />
                  </div>
                  <span class="font-mono text-[11px] tabular-nums text-muted-foreground">
                    {{ challenge.progress }}/{{ challenge.target }}
                  </span>
                </div>
              </div>
            </div>
          </template>
          <template v-else>
            <p class="m-0 mb-3 text-sm text-muted-foreground">
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
