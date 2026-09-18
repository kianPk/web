<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Lock, RefreshCw, ShoppingBag } from "lucide-vue-next";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Skeleton } from "~/components/ui/skeleton";
import { Badge } from "~/components/ui/badge";
import { toast } from "~/components/ui/toast";
import { useAuthStore } from "~/stores/AuthStore";
import { formatTomanAmount } from "~/utilities/irrToman";

const { t, locale } = useI18n();
const auth = useAuthStore();
const apiDomain = useRuntimeConfig().public.apiDomain as string;

type ChallengeCard = {
  id: string;
  key: string;
  tier: string;
  target: number;
  progress: number;
  reward_ypoints: number;
  completed: boolean;
  rewarded: boolean;
  match_type: string | null;
  kind: string;
  metric: string | null;
};

type ChallengeResponse = {
  unlocked: boolean;
  subscription: { tier: string; expires_at: string | null } | null;
  day: string;
  challenges: ChallengeCard[];
  products: Array<{
    id: string;
    title: string;
    slug: string;
    description: string;
    price_irr: number;
    subscription_tier: string;
    vip_duration: string | null;
  }>;
};

const loading = ref(true);
const data = ref<ChallengeResponse | null>(null);

const title = computed(() => t("pages.challenges.title"));
const subtitle = computed(() => {
  if (!data.value?.unlocked) return t("pages.challenges.locked_subtitle");
  const tier = data.value.subscription?.tier;
  const label =
    tier === "premium_plus"
      ? t("pages.challenges.tier_plus")
      : t("pages.challenges.tier_premium");
  return t("pages.challenges.unlocked_subtitle", {
    tier: label,
    day: data.value.day,
  });
});

async function refresh() {
  if (!auth.me?.steam_id) {
    data.value = null;
    loading.value = false;
    return;
  }
  loading.value = true;
  try {
    data.value = await $fetch<ChallengeResponse>(
      `https://${apiDomain}/challenges/me`,
      { credentials: "include", query: { _: Date.now() } },
    );
  } catch (error: any) {
    console.error(error);
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: error?.data?.message || error?.message || String(error),
    });
    data.value = null;
  } finally {
    loading.value = false;
  }
}

function challengeTitle(key: string) {
  return t(`pages.challenges.catalog.${key}.title`);
}

function challengeDesc(key: string) {
  return t(`pages.challenges.catalog.${key}.description`);
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

function progressPct(c: ChallengeCard) {
  if (c.target <= 0) return 0;
  return Math.min(100, Math.round((c.progress / c.target) * 100));
}

onMounted(() => {
  void refresh();
});
</script>

<template>
  <PageTransition>
    <div class="mx-auto flex w-full max-w-5xl flex-col gap-8 px-4 pb-16 pt-6">
      <TacticalPageHeader>
        <template #title>{{ title }}</template>
        <template #subtitle>{{ subtitle }}</template>
        <template #actions>
          <Button
            variant="outline"
            size="sm"
            class="gap-2"
            :disabled="loading"
            @click="refresh"
          >
            <RefreshCw class="h-4 w-4" :class="{ 'animate-spin': loading }" />
            {{ $t("pages.challenges.refresh") }}
          </Button>
        </template>
      </TacticalPageHeader>

      <div v-if="!auth.me" class="text-sm text-muted-foreground">
        {{ $t("pages.challenges.login_required") }}
      </div>

      <div v-else-if="loading" class="grid gap-4 md:grid-cols-2">
        <Skeleton class="h-40 w-full" />
        <Skeleton class="h-40 w-full" />
      </div>

      <template v-else-if="data && !data.unlocked">
        <div
          class="flex flex-col items-start gap-4 rounded-lg border border-border/60 bg-muted/20 p-6"
        >
          <div class="flex items-center gap-2 text-sm font-medium">
            <Lock class="h-4 w-4" />
            {{ $t("pages.challenges.locked_title") }}
          </div>
          <p class="max-w-xl text-sm text-muted-foreground">
            {{ $t("pages.challenges.locked_body") }}
          </p>
          <div class="grid w-full gap-3 md:grid-cols-2">
            <div
              v-for="product in data.products"
              :key="product.id"
              class="flex flex-col gap-2 rounded-md border border-border/50 p-4"
            >
              <div class="flex items-center justify-between gap-2">
                <span class="font-semibold">{{ product.title }}</span>
                <Badge variant="secondary">{{
                  product.subscription_tier === "premium_plus"
                    ? $t("pages.challenges.tier_plus")
                    : $t("pages.challenges.tier_premium")
                }}</Badge>
              </div>
              <p class="text-sm text-muted-foreground">
                {{ product.description }}
              </p>
              <div class="mt-auto flex items-center justify-between pt-2">
                <span class="font-mono text-sm">{{
                  formatPrice(product.price_irr)
                }}</span>
                <Button as-child size="sm" class="gap-1.5">
                  <NuxtLink to="/store">
                    <ShoppingBag class="h-3.5 w-3.5" />
                    {{ $t("pages.challenges.buy") }}
                  </NuxtLink>
                </Button>
              </div>
            </div>
          </div>
        </div>
      </template>

      <template v-else-if="data">
        <div class="grid gap-4 md:grid-cols-2">
          <div
            v-for="challenge in data.challenges"
            :key="challenge.id"
            class="flex flex-col gap-3 rounded-lg border border-border/60 p-5"
            :class="
              challenge.completed
                ? 'border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.06)]'
                : 'bg-background/40'
            "
          >
            <div class="flex items-start justify-between gap-3">
              <div>
                <div class="font-semibold leading-snug">
                  {{ challengeTitle(challenge.key) }}
                </div>
                <p class="mt-1 text-sm text-muted-foreground">
                  {{ challengeDesc(challenge.key) }}
                </p>
              </div>
              <Badge v-if="challenge.completed" variant="default">
                {{
                  challenge.rewarded
                    ? $t("pages.challenges.claimed")
                    : $t("pages.challenges.done")
                }}
              </Badge>
            </div>

            <div class="space-y-1.5">
              <div
                class="flex items-center justify-between font-mono text-xs uppercase tracking-wider text-muted-foreground"
              >
                <span
                  >{{ challenge.progress }} / {{ challenge.target }}</span
                >
                <span class="inline-flex items-center gap-1">
                  <img
                    src="/img/ypoint-logo.png"
                    alt=""
                    class="h-3.5 w-3.5 object-contain"
                  />
                  +{{ challenge.reward_ypoints }}
                </span>
              </div>
              <div class="h-1.5 overflow-hidden rounded-full bg-muted">
                <div
                  class="h-full rounded-full bg-[hsl(var(--tac-amber))] transition-all"
                  :style="{ width: `${progressPct(challenge)}%` }"
                />
              </div>
            </div>
          </div>
        </div>

        <p class="text-xs text-muted-foreground">
          {{ $t("pages.challenges.auto_reward_hint") }}
        </p>
      </template>
    </div>
  </PageTransition>
</template>
