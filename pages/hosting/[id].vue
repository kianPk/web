<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import {
  Copy,
  Minus,
  Plus,
  Power,
  RefreshCw,
  RotateCcw,
  Send,
  Terminal,
  Trash2,
} from "lucide-vue-next";
import { useAuthStore } from "~/stores/AuthStore";
import HostedAdminsPanel from "~/components/hosting/HostedAdminsPanel.vue";
import HostedChatAdsPanel from "~/components/hosting/HostedChatAdsPanel.vue";
import HostedGameplayPanel from "~/components/hosting/HostedGameplayPanel.vue";
import HostedVipManagePanel from "~/components/hosting/HostedVipManagePanel.vue";
import HostedVipShopPanel from "~/components/hosting/HostedVipShopPanel.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Skeleton } from "~/components/ui/skeleton";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "~/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import {
  tacticalCtaButtonClasses,
  tacticalTabsListClasses,
  tacticalTabsTriggerClasses,
} from "~/utilities/tacticalClasses";
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
  hostedDurationDays,
  hostedSlotsCost,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import {
  hostedApi,
  hostedConnectCommand,
  hostedErrorMessage,
  type HostedCheckoutResult,
  type HostedOverview,
  type HostedPlan,
  type HostedServer,
  type HostedSlotsQuote,
} from "~/composables/useHostedServers";

const MAPS = [
  "de_dust2",
  "de_mirage",
  "de_inferno",
  "de_nuke",
  "de_ancient",
  "de_anubis",
  "de_overpass",
  "de_train",
  "de_vertigo",
  "cs_office",
  "cs_italy",
];

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const isAdmin = computed(() => auth.isAdmin);
const hostedId = computed(() => String(route.params.id));

const server = ref<HostedServer | null>(null);
const overview = ref<HostedOverview | null>(null);
const loading = ref(true);
const busy = ref<string | null>(null);
const panelTab = ref("overview");

const form = ref({ label: "", connect_password: "", type: "Casual" });

const rconCommand = ref("");
const rconLog = ref<Array<{ command: string; result: string }>>([]);
const mapToLoad = ref("de_mirage");

const renewOpen = ref(false);
const renewPlanId = ref("");
const renewTerms = ref(false);
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

const isActive = computed(() => server.value?.status === "active");
const isOnline = computed(
  () => !!server.value?.enabled && !!server.value?.connected,
);
const liveLabel = computed(() => {
  if (!server.value || !isActive.value) return "";
  if (!server.value.enabled) return t("pages.hosting.panel.stopped");
  if (server.value.connected) return t("pages.hosting.panel.online");
  return t("pages.hosting.panel.starting");
});
const addressLabel = computed(() => {
  if (!server.value?.host || !server.value?.port) return "—";
  return `${server.value.host}:${server.value.port}`;
});
const isActualOwner = computed(
  () =>
    !!server.value &&
    String(server.value.owner_steam_id) === String(auth.me?.steam_id || ""),
);
const isServerOwner = computed(() => {
  if (!server.value) return false;
  if (isAdmin.value) return true;
  return isActualOwner.value;
});
const managingAsSiteAdmin = computed(
  () => isAdmin.value && !!server.value && !isActualOwner.value,
);
const connectCommand = computed(() =>
  server.value ? hostedConnectCommand(server.value) : null,
);
const extraSlots = computed(() => server.value?.extra_slots || 0);
const renewPlans = computed(() =>
  (overview.value?.plans || []).filter(
    (plan) =>
      !!server.value &&
      plan.hosted_slots === server.value.slots - extraSlots.value,
  ),
);
const renewPlan = computed(
  () => renewPlans.value.find((plan) => plan.id === renewPlanId.value) || null,
);

function extrasCost(plan: HostedPlan) {
  if (!overview.value) return { irr: 0, ypoint: 0 };
  return hostedSlotsCost(
    overview.value,
    extraSlots.value,
    hostedDurationDays(plan.duration),
  );
}
function renewPriceIrr(plan: HostedPlan) {
  return Number(plan.price_irr) + extrasCost(plan).irr;
}
function renewPriceYpoint(plan: HostedPlan | null): number | null {
  if (!plan?.price_ypoint) return null;
  const extras = extrasCost(plan);
  if (extraSlots.value > 0 && !extras.ypoint) return null;
  return Number(plan.price_ypoint) + extras.ypoint;
}

const renewYpoint = computed(() => renewPriceYpoint(renewPlan.value));
const canAffordRenew = computed(() => {
  const price = renewYpoint.value || 0;
  return price > 0 && (ypointBalance.value ?? 0) >= price;
});

const slotsOpen = ref(false);
const slotsCount = ref(1);
const slotsTerms = ref(false);
const slotsQuote = ref<HostedSlotsQuote | null>(null);
const slotsQuoteError = ref<string | null>(null);
const slotsRoom = computed(() =>
  Math.max(0, (overview.value?.max_slots || 0) - (server.value?.slots || 0)),
);
const canBuySlots = computed(
  () =>
    isServerOwner.value &&
    isActive.value &&
    (overview.value?.slot_price_irr || 0) > 0 &&
    slotsRoom.value > 0,
);
const canAffordSlots = computed(() => {
  const price = slotsQuote.value?.price_ypoint || 0;
  return price > 0 && (ypointBalance.value ?? 0) >= price;
});

let quoteSeq = 0;
async function refreshSlotsQuote() {
  const seq = ++quoteSeq;
  try {
    const quote = await hostedApi<HostedSlotsQuote>(
      `/hosted-servers/${hostedId.value}/slots-quote?count=${slotsCount.value}`,
    );
    if (seq !== quoteSeq) return;
    slotsQuote.value = quote;
    slotsQuoteError.value = null;
  } catch (error) {
    if (seq !== quoteSeq) return;
    slotsQuote.value = null;
    slotsQuoteError.value = hostedErrorMessage(error);
  }
}

watch(slotsCount, () => {
  if (slotsOpen.value) void refreshSlotsQuote();
});

function stepSlots(delta: number) {
  slotsCount.value = Math.min(
    slotsRoom.value,
    Math.max(1, slotsCount.value + delta),
  );
}

function openSlots() {
  slotsCount.value = 1;
  slotsTerms.value = false;
  slotsQuote.value = null;
  slotsQuoteError.value = null;
  slotsOpen.value = true;
  void refreshSlotsQuote();
}

function buySlots(payWith: "bale" | "ypoint") {
  return run("slots", async () => {
    const result = await hostedApi<HostedCheckoutResult>(
      "/hosted-servers/slots-checkout",
      {
        method: "POST",
        body: {
          hostedServerId: hostedId.value,
          count: slotsQuote.value?.count,
          termsAccepted: true,
          payWith,
        },
      },
    );
    slotsOpen.value = false;
    if (result.paid) {
      toast({ title: t("pages.hosting.slots_buy.added") });
      void refreshYpoints();
      await load(true);
      return;
    }
    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.hosting.slots_buy.checkout_hint"),
    });
    window.open(result.deepLink, "_blank", "noopener,noreferrer");
  });
}
const canRenew = computed(
  () =>
    isServerOwner.value &&
    !!server.value &&
    ["active", "expired", "deleted"].includes(server.value.status),
);

function syncForm() {
  if (!server.value) return;
  form.value = {
    label: server.value.label || "",
    connect_password: server.value.connect_password || "",
    type: server.value.type || "Casual",
  };
}

async function load(silent = false) {
  if (!silent) loading.value = true;
  try {
    server.value = await hostedApi<HostedServer>(
      `/hosted-servers/${hostedId.value}`,
    );
    if (!silent) syncForm();
  } catch (error) {
    if (!silent) {
      server.value = null;
      toast({ variant: "destructive", title: hostedErrorMessage(error) });
    }
  } finally {
    loading.value = false;
  }
}

async function run(
  key: string,
  action: () => Promise<unknown>,
  successTitle?: string,
) {
  if (busy.value) return;
  busy.value = key;
  try {
    await action();
    if (successTitle) {
      toast({ title: successTitle });
    }
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = null;
  }
}

function restart() {
  return run(
    "restart",
    () =>
      hostedApi(`/hosted-servers/${hostedId.value}/restart`, {
        method: "POST",
      }),
    t("pages.hosting.panel.restarting"),
  );
}

function togglePower() {
  const on = !server.value?.enabled;
  return run("power", async () => {
    server.value = await hostedApi<HostedServer>(
      `/hosted-servers/${hostedId.value}/power`,
      { method: "POST", body: { on } },
    );
  });
}

function saveSettings() {
  return run(
    "settings",
    async () => {
      server.value = await hostedApi<HostedServer>(
        `/hosted-servers/${hostedId.value}/settings`,
        {
          method: "POST",
          body: {
            label: form.value.label,
            connect_password: form.value.connect_password,
            type: form.value.type,
          },
        },
      );
      syncForm();
    },
    t("pages.hosting.panel.saved"),
  );
}

async function sendRcon(command: string) {
  const trimmed = command.trim();
  if (!trimmed) return;
  await run("rcon", async () => {
    const { result } = await hostedApi<{ result: string }>(
      `/hosted-servers/${hostedId.value}/rcon`,
      { method: "POST", body: { command: trimmed } },
    );
    rconLog.value = [{ command: trimmed, result }, ...rconLog.value].slice(
      0,
      30,
    );
    if (command === rconCommand.value) {
      rconCommand.value = "";
    }
  });
}

async function copyConnect() {
  if (!connectCommand.value) return;
  await navigator.clipboard.writeText(connectCommand.value);
  toast({ title: t("pages.hosting.panel.copied") });
}

function adminDelete() {
  if (!window.confirm(t("pages.hosting.admin.delete_confirm"))) return;
  return run("delete", async () => {
    await hostedApi(`/hosted-servers/admin/${hostedId.value}/purge`, {
      method: "POST",
    });
    toast({ title: t("pages.hosting.admin.server_deleted") });
    await router.push("/hosting");
  });
}

function openRenew() {
  renewPlanId.value = renewPlans.value[0]?.id || "";
  renewTerms.value = false;
  renewOpen.value = true;
}

function renew(payWith: "bale" | "ypoint" = "bale") {
  return run("renew", async () => {
    const result = await hostedApi<HostedCheckoutResult>(
      "/hosted-servers/checkout",
      {
        method: "POST",
        body: {
          productId: renewPlanId.value,
          hostedServerId: hostedId.value,
          termsAccepted: true,
          payWith,
        },
      },
    );
    renewOpen.value = false;
    if (result.paid) {
      toast({ title: t("pages.hosting.ypoint_renewed") });
      void refreshYpoints();
      await load(true);
      return;
    }
    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.hosting.checkout_hint"),
    });
    window.open(result.deepLink, "_blank", "noopener,noreferrer");
  });
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

let poll: number | undefined;
onMounted(async () => {
  void refreshYpoints();
  await load();
  try {
    overview.value = await hostedApi<HostedOverview>(
      "/hosted-servers/overview",
    );
  } catch {
    overview.value = null;
  }
  poll = window.setInterval(() => void load(true), 15_000);
});
onUnmounted(() => window.clearInterval(poll));
</script>

<template>
  <div class="hosted-panel space-y-8 pb-20">
    <PageTransition>
      <div v-if="loading" class="space-y-4">
        <Skeleton class="h-64 rounded-[1.35rem]" />
        <Skeleton class="h-11 w-96 max-w-full rounded-lg" />
        <div class="grid gap-4 lg:grid-cols-2">
          <Skeleton class="h-64 rounded-2xl" />
          <Skeleton class="h-64 rounded-2xl" />
        </div>
      </div>

      <p v-else-if="!server" class="text-sm text-muted-foreground">
        {{ $t("pages.hosting.panel.not_found") }}
      </p>

      <div v-else class="space-y-7">
        <div
          v-if="managingAsSiteAdmin"
          class="rounded-xl border border-[hsl(var(--tac-amber)/0.4)] bg-[hsl(var(--tac-amber)/0.1)] px-4 py-3 text-sm"
        >
          {{
            $t("pages.hosting.admin.managing_as_admin", {
              name: server.owner_name || server.owner_steam_id,
            })
          }}
        </div>

        <!-- Cinematic status hero -->
        <section class="hosted-hero relative overflow-hidden rounded-[1.35rem] border border-white/[0.08] bg-[linear-gradient(155deg,#141417_0%,#0c0c0f_55%,#101014_100%)] p-5 shadow-[0_30px_80px_-40px_rgba(0,0,0,0.9),inset_0_1px_0_rgba(255,255,255,0.06)] sm:p-7">
          <div
            class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,hsl(var(--tac-amber)/0.16),transparent_55%),radial-gradient(ellipse_at_bottom_left,rgba(255,255,255,0.04),transparent_45%)]"
          />
          <div
            class="pointer-events-none absolute inset-0 opacity-[0.035]"
            style="
              background-image: linear-gradient(
                  rgba(255, 255, 255, 0.5) 1px,
                  transparent 1px
                ),
                linear-gradient(
                  90deg,
                  rgba(255, 255, 255, 0.5) 1px,
                  transparent 1px
                );
              background-size: 28px 28px;
            "
          />
          <div
            class="pointer-events-none absolute -end-10 top-1/2 h-56 w-56 -translate-y-1/2 rounded-full bg-[hsl(var(--tac-amber)/0.12)] blur-3xl"
          />

          <div class="relative space-y-6">
            <div
              class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between"
            >
              <div class="min-w-0 space-y-3">
                <div class="flex flex-wrap items-center gap-2.5">
                  <span
                    class="tac-section-label inline-flex items-center gap-2 text-[0.7rem] font-semibold uppercase tracking-[0.22em] text-muted-foreground"
                  >
                    <span class="text-[hsl(var(--tac-amber))]">◢</span>
                    {{ $t("pages.hosting.title") }}
                  </span>
                  <span
                    v-if="isActive"
                    class="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.04] px-2.5 py-1 text-[0.68rem] font-medium uppercase tracking-[0.14em]"
                    :class="
                      isOnline
                        ? 'text-emerald-300'
                        : 'text-muted-foreground'
                    "
                  >
                    <span
                      class="relative flex h-1.5 w-1.5"
                      :class="isOnline ? 'hosted-pulse' : ''"
                    >
                      <span
                        class="absolute inline-flex h-full w-full rounded-full opacity-75"
                        :class="
                          isOnline ? 'bg-emerald-400' : 'bg-zinc-500'
                        "
                      />
                      <span
                        class="relative inline-flex h-1.5 w-1.5 rounded-full"
                        :class="
                          isOnline ? 'bg-emerald-300' : 'bg-zinc-500'
                        "
                      />
                    </span>
                    {{ liveLabel }}
                  </span>
                  <Badge :variant="hostedStatusVariant(server.status)">
                    {{ $t(`pages.hosting.status.${server.status}`) }}
                  </Badge>
                  <Badge v-if="server.type" variant="outline">
                    {{ $t(`pages.hosting.modes.${server.type}`) }}
                  </Badge>
                </div>

                <h1
                  class="tac-page-title relative m-0 font-sans text-[clamp(1.85rem,4.5vw,3.1rem)] font-bold uppercase leading-[0.92] tracking-[0.02em] [font-stretch:80%]"
                >
                  <span
                    aria-hidden="true"
                    class="tac-page-title-ghost pointer-events-none absolute left-[6px] right-[-6px] top-[6px] select-none overflow-hidden whitespace-nowrap text-transparent [-webkit-text-stroke:1px_hsl(var(--tac-amber)/0.35)]"
                  >
                    {{ server.label || $t("pages.hosting.title") }}
                  </span>
                  <span
                    class="relative bg-[linear-gradient(180deg,hsl(var(--foreground))_0%,hsl(var(--foreground)/0.72)_100%)] bg-clip-text text-transparent"
                  >
                    {{ server.label || $t("pages.hosting.title") }}
                  </span>
                </h1>
                <p class="tac-section-desc m-0 max-w-xl text-sm text-muted-foreground">
                  {{ $t("pages.hosting.panel.subtitle") }}
                </p>
              </div>

              <div class="flex flex-wrap items-center gap-2">
                <button
                  v-if="isActive"
                  type="button"
                  :class="[
                    tacticalCtaButtonClasses,
                    '!h-10 !px-4 !text-[0.68rem]',
                  ]"
                  :disabled="!!busy || !server.enabled"
                  @click="restart"
                >
                  <RotateCcw class="h-3.5 w-3.5" />
                  {{ $t("pages.hosting.panel.restart") }}
                </button>
                <Button
                  v-if="isActive"
                  size="sm"
                  class="h-10"
                  :variant="server.enabled ? 'destructive' : 'default'"
                  :disabled="!!busy"
                  @click="togglePower"
                >
                  <Power class="me-1.5 h-3.5 w-3.5" />
                  {{
                    server.enabled
                      ? $t("pages.hosting.panel.stop")
                      : $t("pages.hosting.panel.start")
                  }}
                </Button>
                <Button
                  v-if="canRenew"
                  size="sm"
                  variant="secondary"
                  class="h-10"
                  :disabled="!!busy || !renewPlans.length"
                  @click="openRenew"
                >
                  <RefreshCw class="me-1.5 h-3.5 w-3.5" />
                  {{ $t("pages.hosting.panel.renew") }}
                </Button>
                <Button
                  v-if="canBuySlots"
                  size="sm"
                  variant="outline"
                  class="h-10"
                  :disabled="!!busy"
                  @click="openSlots"
                >
                  <Plus class="me-1.5 h-3.5 w-3.5" />
                  {{ $t("pages.hosting.slots_buy.button") }}
                </Button>
                <Button
                  v-if="isAdmin"
                  size="sm"
                  variant="ghost"
                  class="h-10 text-destructive hover:bg-destructive/10 hover:text-destructive"
                  :disabled="!!busy"
                  @click="adminDelete"
                >
                  <Trash2 class="me-1.5 h-3.5 w-3.5" />
                  {{ $t("pages.hosting.admin.delete") }}
                </Button>
              </div>
            </div>

            <p
              v-if="server.status_detail"
              class="m-0 rounded-xl border border-amber-500/30 bg-amber-500/[0.08] px-3.5 py-2.5 text-xs leading-relaxed text-amber-100/90"
            >
              {{ server.status_detail }}
            </p>

            <div class="grid gap-3 sm:grid-cols-3">
              <div class="hosted-metric">
                <p class="hosted-metric__label">
                  {{ $t("pages.hosting.panel.address") }}
                </p>
                <p class="hosted-metric__value font-mono" dir="ltr">
                  {{ addressLabel }}
                </p>
              </div>
              <div class="hosted-metric">
                <p class="hosted-metric__label">
                  {{ $t("pages.hosting.panel.expires") }}
                </p>
                <p class="hosted-metric__value">
                  {{ formatHostedDate(server.expires_at, locale) }}
                </p>
              </div>
              <div class="hosted-metric">
                <p class="hosted-metric__label">
                  {{ $t("pages.hosting.panel.players") }}
                </p>
                <p class="hosted-metric__value">
                  <template v-if="server.players !== null">
                    {{ server.players }}/{{ server.slots }}
                    <span
                      v-if="server.map"
                      class="ms-1 font-mono text-[0.8em] text-muted-foreground"
                    >
                      {{ server.map }}
                    </span>
                  </template>
                  <template v-else>
                    {{ $t("pages.hosting.slots", { n: server.slots }) }}
                    <template v-if="extraSlots">
                      ·
                      {{
                        $t("pages.hosting.slots_buy.extra", { n: extraSlots })
                      }}
                    </template>
                  </template>
                </p>
              </div>
            </div>

            <div
              v-if="connectCommand"
              class="group flex items-center gap-3 rounded-xl border border-white/[0.08] bg-black/35 px-3.5 py-3 backdrop-blur-sm transition-colors hover:border-[hsl(var(--tac-amber)/0.35)]"
            >
              <span
                class="shrink-0 text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-[hsl(var(--tac-amber))]"
              >
                {{ $t("pages.hosting.panel.connect") }}
              </span>
              <code
                class="min-w-0 flex-1 truncate font-mono text-xs text-zinc-200"
                dir="ltr"
              >
                {{ connectCommand }}
              </code>
              <Button
                size="icon"
                variant="ghost"
                class="h-9 w-9 shrink-0 rounded-lg text-[hsl(var(--tac-amber))] hover:bg-[hsl(var(--tac-amber)/0.12)]"
                @click="copyConnect"
              >
                <Copy class="h-4 w-4" />
              </Button>
            </div>
          </div>
        </section>

        <Tabs v-model="panelTab" :scroll-floor="false" class="space-y-6">
          <TabsList
            :class="tacticalTabsListClasses"
            class="h-auto w-full flex-wrap justify-start gap-1 p-1.5 sm:w-auto"
          >
            <TabsTrigger
              value="overview"
              :class="[tacticalTabsTriggerClasses, 'px-4 py-2']"
            >
              {{ $t("pages.hosting.panel.tabs.overview") }}
            </TabsTrigger>
            <TabsTrigger
              v-if="server.status !== 'deleted'"
              value="players"
              :class="[tacticalTabsTriggerClasses, 'px-4 py-2']"
            >
              {{ $t("pages.hosting.panel.tabs.players") }}
            </TabsTrigger>
            <TabsTrigger
              v-if="isActive"
              value="shop"
              :class="[tacticalTabsTriggerClasses, 'px-4 py-2']"
            >
              {{ $t("pages.hosting.panel.tabs.shop") }}
            </TabsTrigger>
            <TabsTrigger
              v-if="isActive"
              value="console"
              :class="[tacticalTabsTriggerClasses, 'px-4 py-2']"
            >
              {{ $t("pages.hosting.panel.tabs.console") }}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="overview" class="mt-0 outline-none">
            <div
              v-if="isActive"
              class="grid gap-5 lg:grid-cols-2 lg:items-start"
            >
              <section class="hosted-card space-y-5 p-5 sm:p-6">
                <div class="space-y-1">
                  <h2
                    class="m-0 inline-flex items-center gap-2 text-base font-semibold tracking-tight"
                  >
                    <span
                      class="h-[2px] w-2.5 bg-[hsl(var(--tac-amber))]"
                    />
                    {{ $t("pages.hosting.panel.settings") }}
                  </h2>
                  <p class="m-0 text-xs text-muted-foreground">
                    {{ $t("pages.hosting.panel.settings_restart_note") }}
                  </p>
                </div>
                <div class="space-y-1.5">
                  <Label>{{ $t("pages.hosting.server_name") }}</Label>
                  <Input v-model="form.label" maxlength="64" />
                </div>
                <div class="space-y-1.5">
                  <Label>{{ $t("pages.hosting.panel.password") }}</Label>
                  <Input
                    v-model="form.connect_password"
                    maxlength="32"
                    dir="ltr"
                    :placeholder="
                      $t('pages.hosting.panel.password_placeholder')
                    "
                  />
                </div>
                <div class="space-y-1.5">
                  <Label>{{ $t("pages.hosting.mode") }}</Label>
                  <Select v-model="form.type">
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
                <button
                  type="button"
                  :class="[
                    tacticalCtaButtonClasses,
                    '!h-10 !px-5 !text-[0.68rem]',
                  ]"
                  :disabled="!!busy"
                  @click="saveSettings"
                >
                  {{ $t("pages.hosting.panel.save") }}
                </button>
              </section>

              <HostedGameplayPanel
                :hosted-id="hostedId"
                :gameplay="server.gameplay"
                @updated="(next) => (server = next)"
              />
            </div>
            <p v-else class="m-0 text-sm text-muted-foreground">
              {{ $t("pages.hosting.panel.subtitle") }}
            </p>
          </TabsContent>

          <TabsContent value="players" class="mt-0 space-y-4 outline-none">
            <HostedAdminsPanel
              v-if="server.status !== 'deleted'"
              :hosted-id="hostedId"
            />
            <HostedVipManagePanel
              v-if="isActive && isServerOwner"
              :hosted-id="hostedId"
            />
          </TabsContent>

          <TabsContent value="shop" class="mt-0 outline-none">
            <div class="grid gap-4 lg:grid-cols-2 lg:items-start">
              <HostedChatAdsPanel
                v-if="isActive"
                :hosted-id="hostedId"
                :ads="server.chat_ads"
                @updated="(next) => (server = next)"
              />
              <HostedVipShopPanel
                v-if="isActive && isServerOwner"
                :hosted-id="hostedId"
                :shop="server.vip_shop"
                :wallet-irr="
                  server.owner_irr_balance ?? server.vip_shop?.wallet_irr
                "
                @updated="(next) => (server = next)"
              />
            </div>
          </TabsContent>

          <TabsContent value="console" class="mt-0 outline-none">
            <section
              v-if="isActive"
              class="hosted-card space-y-5 p-5 sm:p-6"
            >
              <h2
                class="m-0 flex items-center gap-2 text-base font-semibold tracking-tight"
              >
                <span class="h-[2px] w-2.5 bg-[hsl(var(--tac-amber))]" />
                <Terminal class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
                {{ $t("pages.hosting.panel.console") }}
              </h2>

              <div class="flex flex-wrap items-center gap-2">
                <Select v-model="mapToLoad">
                  <SelectTrigger class="w-44">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem v-for="map in MAPS" :key="map" :value="map">
                      {{ map }}
                    </SelectItem>
                  </SelectContent>
                </Select>
                <Button
                  size="sm"
                  variant="outline"
                  :disabled="!!busy || !server.connected"
                  @click="sendRcon(`changelevel ${mapToLoad}`)"
                >
                  {{ $t("pages.hosting.panel.change_map") }}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  :disabled="!!busy || !server.connected"
                  @click="sendRcon('mp_restartgame 1')"
                >
                  {{ $t("pages.hosting.panel.restart_game") }}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  :disabled="!!busy || !server.connected"
                  @click="sendRcon('status')"
                >
                  status
                </Button>
              </div>

              <form
                class="flex gap-2"
                dir="ltr"
                @submit.prevent="sendRcon(rconCommand)"
              >
                <Input
                  v-model="rconCommand"
                  class="font-mono"
                  maxlength="512"
                  placeholder="mp_roundtime 2"
                />
                <Button
                  type="submit"
                  size="sm"
                  :disabled="!!busy || !server.connected || !rconCommand.trim()"
                >
                  <Send class="h-3.5 w-3.5" />
                </Button>
              </form>

              <div
                v-if="rconLog.length"
                class="max-h-80 space-y-2 overflow-y-auto rounded-xl bg-black/80 p-3 font-mono text-xs text-green-300"
                dir="ltr"
              >
                <div v-for="(entry, index) in rconLog" :key="index">
                  <div class="text-white">&gt; {{ entry.command }}</div>
                  <pre class="m-0 whitespace-pre-wrap">{{
                    entry.result || "(ok)"
                  }}</pre>
                </div>
              </div>
            </section>
          </TabsContent>
        </Tabs>
      </div>
    </PageTransition>

    <Dialog v-model:open="renewOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ $t("pages.hosting.panel.renew") }}</DialogTitle>
          <DialogDescription>{{ server?.label }}</DialogDescription>
        </DialogHeader>
        <div class="space-y-3">
          <button
            v-for="plan in renewPlans"
            :key="plan.id"
            type="button"
            class="flex w-full items-center justify-between rounded-lg border px-3 py-2.5 text-start"
            :class="
              renewPlanId === plan.id
                ? 'border-primary/60 bg-primary/5'
                : 'border-border'
            "
            @click="renewPlanId = plan.id"
          >
            <span class="text-sm font-medium">
              {{ plan.title }} · {{ formatHostedDuration(plan.duration, t) }}
            </span>
            <span
              class="flex flex-col items-end font-mono text-sm tabular-nums"
            >
              {{ formatPrice(renewPriceIrr(plan)) }}
              <span
                v-if="renewPriceYpoint(plan)"
                class="inline-flex items-center gap-1 text-xs text-[hsl(var(--tac-amber))]"
              >
                <img
                  src="/img/ypoint-logo.png"
                  alt="Ypoint"
                  class="h-3.5 w-3.5 object-contain"
                />
                {{ renewPriceYpoint(plan) }}
              </span>
            </span>
          </button>
          <p v-if="extraSlots" class="m-0 text-xs text-muted-foreground">
            {{ $t("pages.hosting.slots_buy.renew_note", { n: extraSlots }) }}
          </p>
          <button
            type="button"
            class="flex w-full items-start gap-3 rounded-lg border border-border px-3 py-3 text-start"
            @click="renewTerms = !renewTerms"
          >
            <Checkbox
              :model-value="renewTerms"
              class="pointer-events-none mt-0.5"
              tabindex="-1"
            />
            <span class="text-sm leading-snug">
              {{ $t("pages.store.terms_accept") }}
            </span>
          </button>
        </div>
        <DialogFooter class="gap-2">
          <Button variant="ghost" :disabled="!!busy" @click="renewOpen = false">
            {{ $t("common.cancel") }}
          </Button>
          <Button
            v-if="renewYpoint"
            variant="outline"
            :disabled="!!busy || !renewTerms || !canAffordRenew"
            :title="
              canAffordRenew ? undefined : $t('pages.store.ypoint_not_enough')
            "
            @click="renew('ypoint')"
          >
            <img
              src="/img/ypoint-logo.png"
              alt=""
              class="h-4 w-4 object-contain"
            />
            {{ $t("pages.store.pay_with_ypoint", { n: renewYpoint }) }}
          </Button>
          <Button
            :disabled="!!busy || !renewTerms || !renewPlanId"
            @click="renew('bale')"
          >
            {{ $t("pages.store.pay_with_bale") }}
          </Button>
        </DialogFooter>
        <p
          v-if="renewYpoint && ypointBalance !== null"
          class="m-0 text-end text-xs text-muted-foreground"
        >
          {{ $t("pages.store.ypoint_balance_hint", { n: ypointBalance }) }}
        </p>
      </DialogScrollContent>
    </Dialog>

    <Dialog v-model:open="slotsOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ $t("pages.hosting.slots_buy.title") }}</DialogTitle>
          <DialogDescription>
            {{ $t("pages.hosting.slots_buy.description") }}
          </DialogDescription>
        </DialogHeader>
        <div class="space-y-3">
          <div
            class="flex items-center justify-between rounded-lg border border-border px-3 py-2.5"
          >
            <span class="text-sm">
              {{ $t("pages.hosting.slots_buy.count") }}
            </span>
            <div class="flex items-center gap-2" dir="ltr">
              <Button
                size="icon"
                variant="outline"
                class="h-8 w-8"
                :disabled="slotsCount <= 1"
                @click="stepSlots(-1)"
              >
                <Minus class="h-3.5 w-3.5" />
              </Button>
              <span class="w-8 text-center font-mono text-base tabular-nums">
                {{ slotsCount }}
              </span>
              <Button
                size="icon"
                variant="outline"
                class="h-8 w-8"
                :disabled="slotsCount >= slotsRoom"
                @click="stepSlots(1)"
              >
                <Plus class="h-3.5 w-3.5" />
              </Button>
            </div>
          </div>

          <div
            v-if="slotsQuote"
            class="space-y-1.5 rounded-lg border border-border bg-muted/30 px-3 py-2.5 text-sm"
          >
            <div class="flex justify-between">
              <span class="text-muted-foreground">
                {{ $t("pages.hosting.slots_buy.slots_after") }}
              </span>
              <span class="font-mono tabular-nums">
                {{ server?.slots }} → {{ slotsQuote.slots_after }}
              </span>
            </div>
            <div class="flex justify-between">
              <span class="text-muted-foreground">
                {{ $t("pages.hosting.slots_buy.days_left") }}
              </span>
              <span class="font-mono tabular-nums">
                {{ $t("pages.hosting.duration.days", { n: slotsQuote.days }) }}
              </span>
            </div>
            <div class="flex justify-between font-medium">
              <span>{{ $t("pages.hosting.slots_buy.price") }}</span>
              <span class="flex flex-col items-end font-mono tabular-nums">
                {{ formatPrice(slotsQuote.price_irr) }}
                <span
                  v-if="slotsQuote.price_ypoint"
                  class="inline-flex items-center gap-1 text-xs text-[hsl(var(--tac-amber))]"
                >
                  <img
                    src="/img/ypoint-logo.png"
                    alt="Ypoint"
                    class="h-3.5 w-3.5 object-contain"
                  />
                  {{ slotsQuote.price_ypoint }}
                </span>
              </span>
            </div>
            <p class="m-0 text-xs text-muted-foreground">
              {{ $t("pages.hosting.slots_buy.restart_note") }}
            </p>
          </div>
          <p v-else-if="slotsQuoteError" class="m-0 text-sm text-destructive">
            {{ slotsQuoteError }}
          </p>

          <button
            type="button"
            class="flex w-full items-start gap-3 rounded-lg border border-border px-3 py-3 text-start"
            @click="slotsTerms = !slotsTerms"
          >
            <Checkbox
              :model-value="slotsTerms"
              class="pointer-events-none mt-0.5"
              tabindex="-1"
            />
            <span class="text-sm leading-snug">
              {{ $t("pages.store.terms_accept") }}
            </span>
          </button>
        </div>
        <DialogFooter class="gap-2">
          <Button variant="ghost" :disabled="!!busy" @click="slotsOpen = false">
            {{ $t("common.cancel") }}
          </Button>
          <Button
            v-if="slotsQuote?.price_ypoint"
            variant="outline"
            :disabled="!!busy || !slotsTerms || !canAffordSlots"
            :title="
              canAffordSlots ? undefined : $t('pages.store.ypoint_not_enough')
            "
            @click="buySlots('ypoint')"
          >
            <img
              src="/img/ypoint-logo.png"
              alt=""
              class="h-4 w-4 object-contain"
            />
            {{
              $t("pages.store.pay_with_ypoint", { n: slotsQuote.price_ypoint })
            }}
          </Button>
          <Button
            :disabled="!!busy || !slotsTerms || !slotsQuote"
            @click="buySlots('bale')"
          >
            {{ $t("pages.store.pay_with_bale") }}
          </Button>
        </DialogFooter>
        <p
          v-if="slotsQuote?.price_ypoint && ypointBalance !== null"
          class="m-0 text-end text-xs text-muted-foreground"
        >
          {{ $t("pages.store.ypoint_balance_hint", { n: ypointBalance }) }}
        </p>
      </DialogScrollContent>
    </Dialog>
  </div>
</template>

<style>
.hosted-panel .hosted-card {
  border-radius: 1.15rem;
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: linear-gradient(
    165deg,
    hsl(var(--card) / 0.72) 0%,
    hsl(var(--background) / 0.45) 100%
  );
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.05),
    0 18px 40px -28px rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(10px);
}

.hosted-panel .hosted-metric {
  border-radius: 1rem;
  border: 1px solid rgba(255, 255, 255, 0.07);
  background: rgba(0, 0, 0, 0.28);
  padding: 0.95rem 1.05rem;
  transition:
    border-color 180ms ease,
    background-color 180ms ease,
    transform 180ms ease;
}

.hosted-panel .hosted-metric:hover {
  border-color: hsl(var(--tac-amber) / 0.35);
  background: rgba(0, 0, 0, 0.38);
  transform: translateY(-1px);
}

.hosted-panel .hosted-metric__label {
  margin: 0;
  font-size: 0.64rem;
  font-weight: 650;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: hsl(var(--muted-foreground));
}

.hosted-panel .hosted-metric__value {
  margin: 0.35rem 0 0;
  font-size: 0.95rem;
  font-weight: 600;
  line-height: 1.25;
  color: hsl(var(--foreground));
}

.hosted-panel .hosted-pulse > span:first-child {
  animation: hosted-pulse-ring 1.6s cubic-bezier(0.2, 0.7, 0.3, 1) infinite;
}

@keyframes hosted-pulse-ring {
  0% {
    transform: scale(1);
    opacity: 0.7;
  }
  80% {
    transform: scale(2.4);
    opacity: 0;
  }
  100% {
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .hosted-panel .hosted-pulse > span:first-child {
    animation: none;
  }
  .hosted-panel .hosted-metric:hover {
    transform: none;
  }
}
</style>
