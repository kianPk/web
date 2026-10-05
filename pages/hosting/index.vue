<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Clock, Server, ServerCog, Users } from "lucide-vue-next";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import HostedAdminPanel from "~/components/hosting/HostedAdminPanel.vue";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Skeleton } from "~/components/ui/skeleton";
import Empty from "~/components/ui/empty/Empty.vue";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import {
  Dialog,
  DialogScrollContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "~/components/ui/dialog";
import { toast } from "~/components/ui/toast";
import { formatTomanAmount } from "~/utilities/irrToman";
import {
  formatHostedDate,
  formatHostedDuration,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedCheckoutResult,
  type HostedOverview,
  type HostedPlan,
  type HostedServer,
} from "~/composables/useHostedServers";
import { useAuthStore } from "~/stores/AuthStore";

const { t, locale } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

const overview = ref<HostedOverview | null>(null);
const myServers = ref<HostedServer[]>([]);
const loading = ref(true);

const buyPlan = ref<HostedPlan | null>(null);
const buyOpen = ref(false);
const buyType = ref("Casual");
const buyLabel = ref("");
const termsAccepted = ref(false);
const paying = ref(false);

const signedIn = computed(() => Boolean(auth.me?.steam_id));
const isAdmin = computed(() => auth.isAdmin);

async function refresh() {
  loading.value = true;
  try {
    overview.value = await hostedApi<HostedOverview>(
      "/hosted-servers/overview",
    );
  } catch (error) {
    console.error(error);
    overview.value = null;
  }
  if (signedIn.value) {
    try {
      myServers.value = await hostedApi<HostedServer[]>("/hosted-servers/mine");
    } catch {
      myServers.value = [];
    }
  }
  loading.value = false;
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

function openBuy(plan: HostedPlan) {
  if (!signedIn.value) {
    toast({
      variant: "destructive",
      title: t("pages.store.sign_in_required"),
    });
    return;
  }
  buyPlan.value = plan;
  buyType.value = "Casual";
  buyLabel.value = "";
  termsAccepted.value = false;
  buyOpen.value = true;
}

const canAffordPlan = computed(() => {
  const price = Number(buyPlan.value?.price_ypoint || 0);
  return price > 0 && (ypointBalance.value ?? 0) >= price;
});

async function pay(payWith: "bale" | "ypoint" = "bale") {
  if (!buyPlan.value || paying.value) return;
  if (!termsAccepted.value) {
    toast({ variant: "destructive", title: t("pages.store.terms_required") });
    return;
  }
  paying.value = true;
  try {
    const result = await hostedApi<HostedCheckoutResult>(
      "/hosted-servers/checkout",
      {
        method: "POST",
        body: {
          productId: buyPlan.value.id,
          type: buyType.value,
          label: buyLabel.value,
          termsAccepted: true,
          payWith,
        },
      },
    );
    buyOpen.value = false;
    if (result.paid) {
      toast({ title: t("pages.hosting.ypoint_paid") });
      void refreshYpoints();
      if (result.hostedServerId) {
        await router.push(`/hosting/${result.hostedServerId}`);
      } else {
        await refresh();
      }
      return;
    }
    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.hosting.checkout_hint"),
    });
    window.open(result.deepLink, "_blank", "noopener,noreferrer");
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: hostedErrorMessage(error),
    });
  } finally {
    paying.value = false;
  }
}

onMounted(() => {
  void refresh();
  void refreshYpoints();
});
</script>

<template>
  <div class="space-y-8 pb-16">
    <TacticalPageHeader>
      <template #title>{{ $t("pages.hosting.title") }}</template>
      <template #subtitle>{{ $t("pages.hosting.description") }}</template>
    </TacticalPageHeader>

    <PageTransition>
      <div v-if="loading" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton v-for="i in 3" :key="i" class="h-56 rounded-lg" />
      </div>

      <div v-else class="space-y-8">
        <div
          v-if="overview && (!overview.enabled || !overview.available)"
          class="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm"
        >
          {{
            !overview.enabled
              ? $t("pages.hosting.sales_closed")
              : $t("pages.hosting.capacity_full")
          }}
        </div>
        <p v-else-if="overview" class="m-0 text-xs text-muted-foreground">
          {{ $t("pages.hosting.remaining", { n: overview.remaining }) }}
        </p>

        <Empty v-if="!overview?.plans?.length">
          <h2 class="m-0 text-lg font-semibold">
            {{ $t("pages.hosting.no_plans") }}
          </h2>
        </Empty>

        <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <article
            v-for="plan in overview.plans"
            :key="plan.id"
            class="flex flex-col overflow-hidden rounded-lg border border-border bg-card/40"
          >
            <div class="aspect-[16/9] bg-muted">
              <img
                v-if="plan.image_url"
                :src="plan.image_url"
                :alt="plan.title"
                class="h-full w-full object-cover"
              />
              <div
                v-else
                class="flex h-full w-full items-center justify-center text-muted-foreground"
              >
                <Server class="h-10 w-10 opacity-40" />
              </div>
            </div>
            <div class="flex flex-1 flex-col gap-3 p-4">
              <h2 class="m-0 font-sans text-base font-semibold">
                {{ plan.title }}
              </h2>
              <div class="flex flex-wrap gap-2">
                <Badge variant="secondary">
                  <Users class="h-3 w-3" />
                  {{ $t("pages.hosting.slots", { n: plan.hosted_slots }) }}
                </Badge>
                <Badge variant="outline">
                  <Clock class="h-3 w-3" />
                  {{ formatHostedDuration(plan.duration, t) }}
                </Badge>
              </div>
              <p
                v-if="plan.description"
                class="m-0 whitespace-pre-line text-sm text-muted-foreground"
              >
                {{ plan.description }}
              </p>
              <div class="mt-auto flex items-center justify-between gap-3">
                <div class="flex flex-col gap-0.5">
                  <span class="font-mono text-sm font-semibold tabular-nums">
                    {{ formatPrice(plan.price_irr) }}
                  </span>
                  <span
                    v-if="plan.price_ypoint"
                    class="inline-flex items-center gap-1 font-mono text-xs font-semibold tabular-nums text-[hsl(var(--tac-amber))]"
                  >
                    <img
                      src="/img/ypoint-logo.png"
                      alt="Ypoint"
                      class="h-3.5 w-3.5 object-contain"
                    />
                    {{ plan.price_ypoint }}
                  </span>
                </div>
                <Button
                  type="button"
                  size="sm"
                  :disabled="!overview?.available"
                  @click="openBuy(plan)"
                >
                  {{ $t("pages.hosting.buy") }}
                </Button>
              </div>
            </div>
          </article>
        </div>

        <section v-if="signedIn" class="space-y-3">
          <h2 class="m-0 text-lg font-semibold">
            {{ $t("pages.hosting.my_servers") }}
          </h2>
          <p v-if="!myServers.length" class="m-0 text-sm text-muted-foreground">
            {{ $t("pages.hosting.no_servers") }}
          </p>
          <div v-else class="grid gap-3 sm:grid-cols-2">
            <NuxtLink
              v-for="server in myServers"
              :key="server.id"
              :to="`/hosting/${server.id}`"
              class="flex items-center gap-3 rounded-lg border border-border bg-card/40 p-4 transition-colors hover:bg-muted/40"
            >
              <ServerCog class="h-8 w-8 shrink-0 text-muted-foreground" />
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <span class="truncate font-semibold">{{ server.label }}</span>
                  <Badge :variant="hostedStatusVariant(server.status)">
                    {{ $t(`pages.hosting.status.${server.status}`) }}
                  </Badge>
                  <Badge
                    v-if="
                      String(server.owner_steam_id) !==
                      String(auth.me?.steam_id || '')
                    "
                    variant="outline"
                  >
                    {{ $t("pages.hosting.admins.manager_badge") }}
                  </Badge>
                </div>
                <p class="m-0 mt-1 text-xs text-muted-foreground">
                  {{
                    $t("pages.hosting.expires_at", {
                      date: formatHostedDate(server.expires_at, locale),
                    })
                  }}
                  <span v-if="server.players !== null">
                    · {{ server.players }}/{{ server.slots }}
                  </span>
                </p>
              </div>
            </NuxtLink>
          </div>
        </section>

        <HostedAdminPanel v-if="isAdmin" @changed="refresh" />
      </div>
    </PageTransition>

    <Dialog v-model:open="buyOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ buyPlan?.title }}</DialogTitle>
          <DialogDescription>
            {{ buyPlan ? formatPrice(buyPlan.price_irr) : "" }}
            ·
            {{ buyPlan ? formatHostedDuration(buyPlan.duration, t) : "" }}
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-4">
          <div class="space-y-1.5">
            <Label>{{ $t("pages.hosting.server_name") }}</Label>
            <Input
              v-model="buyLabel"
              maxlength="64"
              :placeholder="$t('pages.hosting.server_name_placeholder')"
            />
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.hosting.mode") }}</Label>
            <Select v-model="buyType">
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem
                  v-for="type in overview?.types || []"
                  :key="type"
                  :value="type"
                >
                  {{ $t(`pages.hosting.modes.${type}`) }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>

          <ol
            class="m-0 list-decimal space-y-1 rounded-lg border border-border bg-muted/20 py-2.5 pe-3 ps-7 text-[0.75rem] leading-relaxed text-muted-foreground"
          >
            <li>{{ $t("pages.hosting.terms_1") }}</li>
            <li>{{ $t("pages.hosting.terms_2") }}</li>
            <li>{{ $t("pages.hosting.terms_3") }}</li>
            <li>{{ $t("pages.store.terms_1") }}</li>
          </ol>

          <button
            type="button"
            class="flex w-full items-start gap-3 rounded-lg border px-3 py-3 text-start"
            :class="
              termsAccepted ? 'border-primary/50 bg-primary/5' : 'border-border'
            "
            @click="termsAccepted = !termsAccepted"
          >
            <Checkbox
              :model-value="termsAccepted"
              class="pointer-events-none mt-0.5"
              tabindex="-1"
            />
            <span class="text-sm leading-snug">
              {{ $t("pages.store.terms_accept") }}
            </span>
          </button>
        </div>

        <DialogFooter class="gap-2">
          <Button
            type="button"
            variant="ghost"
            :disabled="paying"
            @click="buyOpen = false"
          >
            {{ $t("common.cancel") }}
          </Button>
          <Button
            v-if="buyPlan?.price_ypoint"
            type="button"
            variant="outline"
            :disabled="paying || !termsAccepted || !canAffordPlan"
            :title="
              canAffordPlan ? undefined : $t('pages.store.ypoint_not_enough')
            "
            @click="pay('ypoint')"
          >
            <img
              src="/img/ypoint-logo.png"
              alt=""
              class="h-4 w-4 object-contain"
            />
            {{ $t("pages.store.pay_with_ypoint", { n: buyPlan.price_ypoint }) }}
          </Button>
          <Button
            type="button"
            :disabled="paying || !termsAccepted"
            @click="pay('bale')"
          >
            {{
              paying
                ? $t("pages.store.buying")
                : $t("pages.store.pay_with_bale")
            }}
          </Button>
        </DialogFooter>
        <p
          v-if="buyPlan?.price_ypoint && ypointBalance !== null"
          class="m-0 text-end text-xs text-muted-foreground"
        >
          {{ $t("pages.store.ypoint_balance_hint", { n: ypointBalance }) }}
        </p>
      </DialogScrollContent>
    </Dialog>
  </div>
</template>
