<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import gql from "graphql-tag";
import { ArrowLeft, Clock, Server, Settings2 } from "lucide-vue-next";
import getGraphqlClient from "~/graphql/getGraphqlClient";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Skeleton } from "~/components/ui/skeleton";
import {
  Dialog,
  DialogScrollContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "~/components/ui/dialog";
import { toast } from "~/components/ui/toast";
import { formatTomanAmount, irrToToman, tomanToIrr } from "~/utilities/irrToman";
import {
  formatHostedDate,
  formatHostedDuration,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import { SERVER_MODES, type ServerModeKey } from "~/utilities/serverModes";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedAdminSettings,
  type HostedCheckoutResult,
  type HostedServer,
  type SectionHostOverview,
  type SectionHostQuote,
} from "~/composables/useHostedServers";
import { useAuthStore } from "~/stores/AuthStore";

const { t, locale } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

/** Same settings table other admin pages write via Hasura (works without API redeploy). */
const HOST_RATE_SETTING = {
  irr: "hosted_servers.section_host_price_irr_per_hour",
  yp: "hosted_servers.section_host_price_ypoint_per_hour",
  slotIrr: "hosted_servers.section_host_slot_price_irr_per_hour",
  slotYp: "hosted_servers.section_host_slot_price_ypoint_per_hour",
} as const;

const HOST_RATE_NAMES = Object.values(HOST_RATE_SETTING);
const HOST_RATES_LOCAL_KEY = "yguard:host-section-rates";

const LOAD_HOST_RATES = gql`
  query HostSectionRates($names: [String!]!) {
    settings(where: { name: { _in: $names } }) {
      name
      value
    }
  }
`;

const SAVE_HOST_RATES = gql`
  mutation SaveHostSectionRates($objects: [settings_insert_input!]!) {
    insert_settings(
      objects: $objects
      on_conflict: { constraint: settings_pkey, update_columns: [value] }
    ) {
      affected_rows
    }
  }
`;

type HostRateValues = {
  irr: number;
  yp: number;
  slotIrr: number;
  slotYp: number;
};

useHead(() => ({ title: t("pages.servers.host.title") }));

const overview = ref<SectionHostOverview | null>(null);
const quote = ref<SectionHostQuote | null>(null);
const quoteError = ref<string | null>(null);
const myServers = ref<HostedServer[]>([]);
const loading = ref(true);
const quoting = ref(false);
let quoteSeq = 0;
let quoteTimer: ReturnType<typeof setTimeout> | null = null;

const FALLBACK_DURATIONS = [
  "1h",
  "3h",
  "12h",
  "1d",
  "3d",
  "7d",
  "14d",
  "30d",
] as const;

/** Slots included in the base Host hourly rate. */
const HOST_INCLUDED_SLOTS = 10;

const mode = ref<ServerModeKey>("duels");
const duration = ref<string>("1h");
const label = ref("");
const slots = ref(HOST_INCLUDED_SLOTS);
const connectPassword = ref("");
const adminSteamId = ref("");
const termsAccepted = ref(false);
const paying = ref(false);
const checkoutOpen = ref(false);

const rateToman = ref(0);
const rateYpoint = ref(0);
const slotRateToman = ref(0);
const slotRateYpoint = ref(0);
const rateSaving = ref(false);

const signedIn = computed(() => Boolean(auth.me?.steam_id));
const isAdmin = computed(() => auth.isAdmin);

const modeOptions = computed(() => {
  const allowed = new Set(
    overview.value?.modes || SERVER_MODES.map((m) => m.key),
  );
  return SERVER_MODES.filter((m) => allowed.has(m.key));
});

const durationOptions = computed(() => {
  const raw = overview.value?.durations?.length
    ? overview.value.durations
    : [...FALLBACK_DURATIONS];
  const normalized = raw.map(normalizeDuration).filter(Boolean) as string[];
  return normalized.length ? normalized : [...FALLBACK_DURATIONS];
});

const selectedMode = computed(
  () => modeOptions.value.find((m) => m.key === mode.value) || modeOptions.value[0],
);

const maxSlots = computed(() => overview.value?.max_slots || 32);

const includedSlots = computed(
  () => overview.value?.included_slots || HOST_INCLUDED_SLOTS,
);

const safeSlots = computed(() => {
  const n = Math.floor(Number(slots.value));
  if (!Number.isFinite(n)) return includedSlots.value;
  return Math.min(maxSlots.value, Math.max(includedSlots.value, n));
});

const safeDuration = computed(() => normalizeDuration(duration.value) || "1h");

/** Price always from local rates (admin form / overview). API quote is optional. */
const displayQuote = computed<SectionHostQuote | null>(() => {
  const local = computeLocalQuote();
  const remote = quote.value;
  if (
    remote &&
    Number(remote.price_irr) > 0 &&
    normalizeDuration(remote.duration) === safeDuration.value
  ) {
    return {
      ...local!,
      ...remote,
      base_price_irr: remote.base_price_irr ?? remote.price_irr,
      slots_price_irr: remote.slots_price_irr ?? 0,
      extra_slots: remote.extra_slots ?? local?.extra_slots ?? 0,
    };
  }
  return local;
});

const canRent = computed(() => {
  const price = Number(displayQuote.value?.price_irr || 0);
  if (price <= 0) return false;
  // Only block when the API explicitly says closed/full — missing fields must not disable Rent.
  if (overview.value?.enabled === false) return false;
  if (overview.value?.available === false) return false;
  return true;
});

const myHostServers = computed(() =>
  myServers.value.filter((s) => Boolean(s.section_mode)),
);

const canAffordYpoint = computed(() => {
  const price = Number(displayQuote.value?.price_ypoint || 0);
  return price > 0 && (ypointBalance.value ?? 0) >= price;
});

/** Accept "1h" / "1 h" / bare "1" (treat as hours) / "1d". */
function normalizeDuration(value: string | number | null | undefined): string {
  const raw = String(value ?? "")
    .trim()
    .toLowerCase();
  if (!raw) return "1h";
  const m = raw.match(
    /^(\d+)\s*(h|hr|hrs|d|day|days|w|week|weeks|mo|month|months)?$/,
  );
  if (!m) return "1h";
  const n = Number(m[1]);
  if (!Number.isFinite(n) || n < 1) return "1h";
  const unit = m[2] || "h";
  if (/^(h|hr|hrs)$/.test(unit)) return `${n}h`;
  if (/^(d|day|days)$/.test(unit)) return `${n}d`;
  if (/^(w|week|weeks)$/.test(unit)) return `${n}w`;
  return `${n}mo`;
}

function durationHours(value: string): number {
  const clean = normalizeDuration(value);
  const m = clean.match(/^(\d+)(h|d|w|mo)$/);
  if (!m) return 24;
  const n = Number(m[1]);
  if (m[2] === "h") return Math.max(1, n);
  if (m[2] === "d") return Math.max(1, n * 24);
  if (m[2] === "w") return Math.max(1, n * 7 * 24);
  return Math.max(1, n * 30 * 24);
}

function computeLocalQuote(): SectionHostQuote | null {
  if (!overview.value && !rateToman.value && !rateYpoint.value) return null;
  const dur = safeDuration.value;
  const hours = durationHours(dur);
  const included = includedSlots.value;
  const slotCount = safeSlots.value;
  const extra = Math.max(0, slotCount - included);
  // Prefer live admin inputs so price updates even before/without overview rates.
  const rateIrr =
    tomanToIrr(rateToman.value) ||
    overview.value?.price_irr_per_hour ||
    500_000;
  const rateYp =
    Number(rateYpoint.value) ||
    overview.value?.price_ypoint_per_hour ||
    0;
  const slotIrrPerHour =
    tomanToIrr(slotRateToman.value) ||
    overview.value?.slot_price_irr_per_hour ||
    overview.value?.slot_price_irr ||
    50_000;
  const slotYpPerHour =
    Number(slotRateYpoint.value) ||
    overview.value?.slot_price_ypoint_per_hour ||
    overview.value?.slot_price_ypoint ||
    5;
  const baseIrr = Math.ceil((rateIrr * hours) / 10_000) * 10_000;
  const baseYp = rateYp > 0 ? Math.ceil(rateYp * hours) : 0;
  const slotsIrr =
    extra > 0 && slotIrrPerHour > 0
      ? Math.ceil((slotIrrPerHour * extra * hours) / 10_000) * 10_000
      : 0;
  const slotsYp =
    extra > 0 && slotYpPerHour > 0
      ? Math.ceil(slotYpPerHour * extra * hours)
      : 0;
  return {
    duration: dur,
    hours,
    slots: slotCount,
    included_slots: included,
    extra_slots: extra,
    base_price_irr: baseIrr,
    base_price_ypoint: baseYp,
    slots_price_irr: slotsIrr,
    slots_price_ypoint: slotsYp,
    price_irr: baseIrr + slotsIrr,
    price_ypoint: baseYp + slotsYp,
    price_irr_per_hour: rateIrr,
    price_ypoint_per_hour: rateYp,
    max_slots: maxSlots.value,
  };
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

/** Parse admin rate inputs (custom Input may emit strings; allow Latin digits). */
function parseRate(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value)) {
    return Math.max(0, Math.round(value));
  }
  const raw = String(value ?? "")
    .trim()
    .replace(/,/g, "")
    .replace(/[۰-۹]/g, (d) => String("۰۱۲۳۴۵۶۷۸۹".indexOf(d)));
  if (!raw) return 0;
  const n = Number(raw);
  return Number.isFinite(n) ? Math.max(0, Math.round(n)) : 0;
}

function currentRateValues(): HostRateValues {
  return {
    irr: tomanToIrr(parseRate(rateToman.value)),
    yp: parseRate(rateYpoint.value),
    slotIrr: tomanToIrr(parseRate(slotRateToman.value)),
    slotYp: parseRate(slotRateYpoint.value),
  };
}

function persistLocalRates(values: HostRateValues) {
  try {
    localStorage.setItem(HOST_RATES_LOCAL_KEY, JSON.stringify(values));
  } catch {
    /* ignore quota / private mode */
  }
}

function readLocalRates(): HostRateValues | null {
  try {
    const raw = localStorage.getItem(HOST_RATES_LOCAL_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<HostRateValues>;
    const irr = Number(parsed.irr);
    const yp = Number(parsed.yp);
    const slotIrr = Number(parsed.slotIrr);
    const slotYp = Number(parsed.slotYp);
    if (![irr, yp, slotIrr, slotYp].every((n) => Number.isFinite(n) && n >= 0)) {
      return null;
    }
    return { irr, yp, slotIrr, slotYp };
  } catch {
    return null;
  }
}

function applyRateValues(input: {
  irr?: number;
  yp?: number;
  slotIrr?: number;
  slotYp?: number;
}) {
  // Treat 0 from a stale API overview as "missing" so it cannot wipe real rates.
  const pick = (n: unknown, fallback: number) => {
    const v = Number(n);
    return Number.isFinite(v) && v > 0 ? v : fallback;
  };
  const ypPick = (n: unknown, fallback: number) => {
    const v = Number(n);
    return Number.isFinite(v) && v >= 0 ? v : fallback;
  };
  rateToman.value = irrToToman(pick(input.irr, 500_000));
  rateYpoint.value = ypPick(input.yp, 50);
  slotRateToman.value = irrToToman(pick(input.slotIrr, 50_000));
  slotRateYpoint.value = ypPick(input.slotYp, 5);
  if (overview.value) {
    overview.value.price_irr_per_hour = tomanToIrr(rateToman.value);
    overview.value.price_ypoint_per_hour = rateYpoint.value;
    overview.value.slot_price_irr_per_hour = tomanToIrr(slotRateToman.value);
    overview.value.slot_price_ypoint_per_hour = slotRateYpoint.value;
  }
}

function graphqlErrorMessage(error: unknown): string {
  const err = error as {
    message?: string;
    graphQLErrors?: Array<{ message?: string }>;
    networkError?: { message?: string };
  };
  const gqlMsg = err?.graphQLErrors?.map((e) => e.message).filter(Boolean).join(", ");
  return gqlMsg || err?.networkError?.message || err?.message || hostedErrorMessage(error);
}

async function loadHostRatesFromHasura(): Promise<boolean> {
  const { data } = await getGraphqlClient().query<{
    settings: Array<{ name: string; value: string | null }>;
  }>({
    query: LOAD_HOST_RATES,
    variables: { names: HOST_RATE_NAMES },
    fetchPolicy: "network-only",
  });
  const rows = data?.settings || [];
  if (!rows.length) return false;
  const read = (name: string) => {
    const raw = rows.find((r) => r.name === name)?.value;
    if (raw == null || raw === "") return undefined;
    const n = Number.parseInt(String(raw), 10);
    return Number.isFinite(n) ? n : undefined;
  };
  const values: HostRateValues = {
    irr: read(HOST_RATE_SETTING.irr) ?? 500_000,
    yp: read(HOST_RATE_SETTING.yp) ?? 50,
    slotIrr: read(HOST_RATE_SETTING.slotIrr) ?? 50_000,
    slotYp: read(HOST_RATE_SETTING.slotYp) ?? 5,
  };
  // Only accept a Hasura row set if at least one money rate is present in DB.
  const hasMoney = rows.some(
    (r) =>
      (r.name === HOST_RATE_SETTING.irr || r.name === HOST_RATE_SETTING.slotIrr) &&
      String(r.value ?? "").length > 0,
  );
  if (!hasMoney) return false;
  applyRateValues(values);
  persistLocalRates(values);
  return true;
}

async function loadAdminRates(): Promise<void> {
  try {
    if (await loadHostRatesFromHasura()) return;
  } catch (error) {
    console.warn("host rates hasura load failed", error);
  }
  const local = readLocalRates();
  if (local) {
    applyRateValues(local);
    return;
  }
  try {
    const admin = await hostedApi<HostedAdminSettings>(
      "/hosted-servers/admin/settings",
    );
    applyRateValues({
      irr: admin.section_host_price_irr_per_hour,
      yp: admin.section_host_price_ypoint_per_hour,
      slotIrr: admin.section_host_slot_price_irr_per_hour,
      slotYp: admin.section_host_slot_price_ypoint_per_hour,
    });
  } catch {
    /* keep whatever is already in the form */
  }
}

async function refreshOverview() {
  overview.value = await hostedApi<SectionHostOverview>(
    "/hosted-servers/section-host/overview",
  );
  duration.value = normalizeDuration(
    durationOptions.value.includes(normalizeDuration(duration.value))
      ? duration.value
      : durationOptions.value[0],
  );
  if (overview.value.modes?.length && !overview.value.modes.includes(mode.value)) {
    mode.value = overview.value.modes[0] as ServerModeKey;
  }
  // Wait for session so administrator Hasura permissions apply.
  await auth.getMe();
  if (isAdmin.value) {
    await loadAdminRates();
  } else {
    // Non-admin: overview only, ignore zero placeholders from old API builds.
    const ov = overview.value;
    if (Number(ov.price_irr_per_hour) > 0 || Number(ov.price_ypoint_per_hour) > 0) {
      applyRateValues({
        irr: ov.price_irr_per_hour,
        yp: ov.price_ypoint_per_hour,
        slotIrr: ov.slot_price_irr_per_hour ?? ov.slot_price_irr,
        slotYp: ov.slot_price_ypoint_per_hour ?? ov.slot_price_ypoint,
      });
    }
  }
  syncSlotsToMode();
}

function syncSlotsToMode() {
  // Always start at the included base (10). User can add more.
  slots.value = includedSlots.value;
}

function scheduleQuote() {
  if (quoteTimer) clearTimeout(quoteTimer);
  quoteTimer = setTimeout(() => {
    void refreshQuote();
  }, 150);
}

async function refreshQuote() {
  const seq = ++quoteSeq;
  duration.value = safeDuration.value;
  const slotCount = safeSlots.value;
  if (slots.value !== slotCount) {
    slots.value = slotCount;
  }
  // Local price is the source of truth for the UI (works even if quote GET 404s).
  quote.value = computeLocalQuote();
  quoting.value = true;
  quoteError.value = null;
  try {
    const params = new URLSearchParams({
      duration: safeDuration.value,
      slots: String(slotCount),
      sectionMode: mode.value,
    });
    const remote = await hostedApi<SectionHostQuote>(
      `/hosted-servers/section-host/quote?${params}`,
    );
    if (seq !== quoteSeq) return;
    if (remote && Number(remote.price_irr) > 0) {
      quote.value = remote;
    }
  } catch (error) {
    if (seq !== quoteSeq) return;
    // Silent fallback — do not block the Rent button on a missing quote route.
    quote.value = computeLocalQuote();
    const msg = hostedErrorMessage(error);
    if (!/cannot get|404|not found/i.test(msg)) {
      quoteError.value = msg;
    }
    console.warn("section-host quote unavailable, using local price", error);
  } finally {
    if (seq === quoteSeq) quoting.value = false;
  }
}

async function refresh() {
  loading.value = true;
  try {
    await refreshOverview();
    await refreshQuote();
  } catch (error) {
    console.error(error);
    overview.value = null;
    quote.value = null;
    quoteError.value = hostedErrorMessage(error);
  }
  if (signedIn.value) {
    try {
      myServers.value = await hostedApi<HostedServer[]>("/hosted-servers/mine");
    } catch {
      myServers.value = [];
    }
  }
  loading.value = false;
  // Re-apply rates after the admin section mounts — the previous shadcn Input
  // stack could emit an empty update on mount and wipe values set while loading.
  await nextTick();
  if (isAdmin.value) {
    await loadAdminRates();
  }
}

watch(
  [duration, slots, mode, rateToman, rateYpoint, slotRateToman, slotRateYpoint],
  () => {
    duration.value = normalizeDuration(duration.value);
    scheduleQuote();
  },
);

watch(mode, () => {
  // Mode change does not reset slots above the included base if user already raised them.
  if (slots.value < includedSlots.value) {
    slots.value = includedSlots.value;
  }
});

function pickDuration(d: string) {
  duration.value = normalizeDuration(d);
}

function openCheckout() {
  if (!signedIn.value) {
    toast({
      variant: "destructive",
      title: t("pages.store.sign_in_required"),
    });
    return;
  }
  if (overview.value?.enabled === false) {
    toast({
      variant: "destructive",
      title: t("pages.servers.host.sales_closed"),
    });
    return;
  }
  if (overview.value?.available === false) {
    toast({
      variant: "destructive",
      title: t("pages.servers.host.capacity_full"),
    });
    return;
  }
  termsAccepted.value = false;
  checkoutOpen.value = true;
}

async function pay(payWith: "bale" | "ypoint" = "bale") {
  if (paying.value || !displayQuote.value) return;
  if (!termsAccepted.value) {
    toast({ variant: "destructive", title: t("pages.store.terms_required") });
    return;
  }
  paying.value = true;
  try {
    const checkoutBody = {
      sectionMode: mode.value,
      duration: safeDuration.value,
      label: label.value,
      slots: safeSlots.value,
      connectPassword: connectPassword.value.trim() || undefined,
      adminSteamId: adminSteamId.value.trim() || undefined,
      termsAccepted: true,
      payWith,
    };
    // Prefer the long-lived /checkout path (Hosting already uses it), then aliases.
    const checkoutPaths = [
      "/hosted-servers/checkout",
      "/hosted-servers/host-checkout",
      "/hosted-servers/section-host-checkout",
      "/hosted-servers/section-host/checkout",
    ];
    let result: HostedCheckoutResult | null = null;
    let lastCheckoutError: unknown;
    for (const path of checkoutPaths) {
      try {
        result = await hostedApi<HostedCheckoutResult>(path, {
          method: "POST",
          body: checkoutBody,
        });
        break;
      } catch (error) {
        lastCheckoutError = error;
        const msg = hostedErrorMessage(error);
        if (!/cannot post|404|not found/i.test(msg)) throw error;
      }
    }
    if (!result) {
      throw lastCheckoutError || new Error("Host checkout unavailable");
    }
    checkoutOpen.value = false;
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
      description: t("pages.servers.host.checkout_hint"),
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

async function saveRates() {
  if (rateSaving.value) return;
  rateSaving.value = true;
  try {
    await auth.getMe();
    if (!isAdmin.value) {
      throw new Error(t("pages.store.sign_in_required"));
    }
    const values = currentRateValues();
    if (values.irr <= 0) {
      throw new Error(t("pages.servers.host.admin.rate_required"));
    }
    // Keep a browser backup so a refresh never blanks the admin form even if
    // Hasura is momentarily unavailable.
    persistLocalRates(values);
    applyRateValues(values);

    const result = await getGraphqlClient().mutate<{
      insert_settings?: { affected_rows?: number };
    }>({
      mutation: SAVE_HOST_RATES,
      variables: {
        objects: [
          { name: HOST_RATE_SETTING.irr, value: String(values.irr) },
          { name: HOST_RATE_SETTING.yp, value: String(values.yp) },
          { name: HOST_RATE_SETTING.slotIrr, value: String(values.slotIrr) },
          { name: HOST_RATE_SETTING.slotYp, value: String(values.slotYp) },
        ],
      },
    });
    const affected = Number(result.data?.insert_settings?.affected_rows || 0);
    if (affected < 1) {
      throw new Error(t("pages.servers.host.admin.rates_not_persisted"));
    }
    // Confirm round-trip from Hasura (same client as Application settings).
    await loadHostRatesFromHasura();
    toast({ title: t("pages.servers.host.admin.rates_saved") });
    await refreshQuote();
  } catch (error) {
    toast({
      variant: "destructive",
      title: graphqlErrorMessage(error),
    });
  } finally {
    rateSaving.value = false;
  }
}

watch(isAdmin, (admin) => {
  if (admin) void loadAdminRates();
});

onMounted(() => {
  void (async () => {
    await auth.getMe();
    await refresh();
    void refreshYpoints();
  })();
});
</script>

<template>
  <div class="space-y-8 pb-16">
    <div class="space-y-3">
      <NuxtLink
        to="/game-servers"
        class="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft class="h-4 w-4" />
        {{ $t("pages.servers.back_to_modes") }}
      </NuxtLink>
      <div class="space-y-1">
        <h1 class="m-0 text-2xl font-semibold tracking-tight">
          {{ $t("pages.servers.host.title") }}
        </h1>
        <p class="m-0 max-w-2xl text-sm text-muted-foreground">
          {{ $t("pages.servers.host.description_config") }}
        </p>
      </div>
    </div>

    <PageTransition>
      <div v-if="loading" class="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Skeleton class="h-80 rounded-2xl" />
        <Skeleton class="h-80 rounded-2xl" />
      </div>

      <div v-else class="space-y-8">
        <section
          v-if="isAdmin"
          class="space-y-3 rounded-2xl border border-border/70 bg-card/30 p-4"
        >
          <div class="flex items-center gap-2">
            <Settings2 class="h-4 w-4 text-muted-foreground" />
            <h2 class="m-0 text-base font-semibold">
              {{ $t("pages.servers.host.admin.rates_title") }}
            </h2>
          </div>
          <p class="m-0 text-xs text-muted-foreground">
            {{ $t("pages.servers.host.admin.rates_hint") }}
          </p>
          <div class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1.5">
              <Label for="host-rate-toman">{{
                $t("pages.servers.host.admin.rate_toman")
              }}</Label>
              <!-- Native inputs: shadcn Input + useVModel(passive) remounts after
                   loading and emits "" which was wiping saved rates to 0. -->
              <input
                id="host-rate-toman"
                v-model.number="rateToman"
                type="number"
                min="0"
                step="1"
                autocomplete="off"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>
            <div class="space-y-1.5">
              <Label for="host-rate-ypoint">{{
                $t("pages.servers.host.admin.rate_ypoint")
              }}</Label>
              <input
                id="host-rate-ypoint"
                v-model.number="rateYpoint"
                type="number"
                min="0"
                step="1"
                autocomplete="off"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>
            <div class="space-y-1.5">
              <Label for="host-slot-rate-toman">{{
                $t("pages.servers.host.admin.slot_rate_toman")
              }}</Label>
              <input
                id="host-slot-rate-toman"
                v-model.number="slotRateToman"
                type="number"
                min="0"
                step="1"
                autocomplete="off"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>
            <div class="space-y-1.5">
              <Label for="host-slot-rate-ypoint">{{
                $t("pages.servers.host.admin.slot_rate_ypoint")
              }}</Label>
              <input
                id="host-slot-rate-ypoint"
                v-model.number="slotRateYpoint"
                type="number"
                min="0"
                step="1"
                autocomplete="off"
                class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              />
            </div>
          </div>
          <Button type="button" size="sm" :disabled="rateSaving" @click="saveRates">
            {{ $t("pages.servers.host.admin.save_rates") }}
          </Button>
        </section>

        <div
          v-if="overview && overview.enabled === false"
          class="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm"
        >
          {{ $t("pages.servers.host.sales_closed") }}
        </div>
        <div
          v-else-if="overview && overview.available === false"
          class="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm"
        >
          {{ $t("pages.servers.host.capacity_full") }}
        </div>
        <p
          v-else-if="overview && overview.remaining != null"
          class="m-0 text-xs text-muted-foreground"
        >
          {{ $t("pages.servers.host.remaining", { n: overview.remaining }) }}
        </p>

        <div class="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <section
            class="space-y-5 rounded-2xl border border-border/70 bg-card/40 p-5"
          >
            <div>
              <h2 class="m-0 text-lg font-semibold">
                {{ $t("pages.servers.host.configure") }}
              </h2>
              <p class="m-0 mt-1 text-sm text-muted-foreground">
                {{ $t("pages.servers.host.configure_hint") }}
              </p>
            </div>

            <div class="space-y-2">
              <Label>{{ $t("pages.servers.host.pick_mode") }}</Label>
              <div class="grid grid-cols-2 gap-2.5 sm:grid-cols-3">
                <button
                  v-for="m in modeOptions"
                  :key="m.key"
                  type="button"
                  class="group relative flex min-h-[5.5rem] flex-col justify-between overflow-hidden rounded-xl border p-3 text-start transition duration-200"
                  :class="
                    mode === m.key
                      ? 'border-[hsl(var(--tac-amber))] bg-[hsl(var(--tac-amber)/0.12)] ring-2 ring-[hsl(var(--tac-amber)/0.3)]'
                      : 'border-border/70 bg-card/50 hover:border-white/25 hover:bg-card/80'
                  "
                  @click="mode = m.key"
                >
                  <span
                    class="pointer-events-none absolute -end-4 -top-4 h-16 w-16 rounded-full opacity-30 blur-2xl"
                    :style="{ background: m.accent }"
                    aria-hidden="true"
                  />
                  <span
                    class="relative inline-flex h-2 w-2 rounded-full"
                    :style="{ background: m.accent }"
                  />
                  <div class="relative mt-2">
                    <div class="text-sm font-semibold text-foreground">
                      {{ $t(`pages.servers.modes.${m.key}.name`) }}
                    </div>
                    <div class="text-[11px] text-muted-foreground">
                      {{
                        $t("pages.servers.host.mode_slots_hint", {
                          n: HOST_INCLUDED_SLOTS,
                        })
                      }}
                    </div>
                  </div>
                </button>
              </div>
            </div>

            <div class="space-y-2">
              <Label>{{ $t("pages.servers.host.admin.duration") }}</Label>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="d in durationOptions"
                  :key="d"
                  type="button"
                  class="rounded-lg border px-3 py-2 text-sm transition-colors"
                  :class="
                    safeDuration === normalizeDuration(d)
                      ? 'border-primary bg-primary/10 font-semibold'
                      : 'border-border hover:bg-muted/40'
                  "
                  @click="pickDuration(d)"
                >
                  {{ formatHostedDuration(d, t) }}
                </button>
              </div>
            </div>

            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.server_name") }}</Label>
              <Input
                v-model="label"
                maxlength="64"
                :placeholder="$t('pages.servers.host.name_placeholder')"
              />
            </div>

            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.slots") }}</Label>
              <div class="flex items-center gap-2">
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  class="h-9 w-9"
                  :disabled="slots <= includedSlots"
                  @click="slots = Math.max(includedSlots, slots - 1)"
                >
                  −
                </Button>
                <Input
                  v-model.number="slots"
                  type="number"
                  min="2"
                  :max="maxSlots"
                  class="text-center font-mono"
                />
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  class="h-9 w-9"
                  :disabled="slots >= maxSlots"
                  @click="slots = Math.min(maxSlots, slots + 1)"
                >
                  +
                </Button>
              </div>
              <p class="m-0 text-xs text-muted-foreground">
                {{
                  $t("pages.servers.host.slots_hint", {
                    included: includedSlots,
                    max: maxSlots,
                  })
                }}
              </p>
            </div>

            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.password") }}</Label>
              <Input
                v-model="connectPassword"
                maxlength="32"
                autocomplete="off"
                :placeholder="$t('pages.servers.host.password_placeholder')"
              />
              <p class="m-0 text-xs text-muted-foreground">
                {{ $t("pages.servers.host.password_hint") }}
              </p>
            </div>

            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.admin_steam") }}</Label>
              <Input
                v-model="adminSteamId"
                maxlength="80"
                :placeholder="$t('pages.servers.host.admin_steam_placeholder')"
              />
              <p class="m-0 text-xs text-muted-foreground">
                {{ $t("pages.servers.host.admin_steam_hint") }}
              </p>
            </div>
          </section>

          <aside
            class="relative flex flex-col gap-4 overflow-hidden rounded-2xl border border-border/70 bg-[#0a0c10] p-5 text-white ring-1 ring-white/5"
          >
            <img
              v-if="selectedMode"
              :src="selectedMode.cover"
              alt=""
              class="pointer-events-none absolute inset-0 h-full w-full object-cover opacity-25"
            />
            <div
              class="pointer-events-none absolute inset-0 bg-gradient-to-b from-black/40 via-[#0a0c10]/90 to-[#0a0c10]"
            />
            <div class="relative space-y-1">
              <p class="m-0 text-xs uppercase tracking-[0.16em] text-white/45">
                {{ $t("pages.servers.host.summary") }}
              </p>
              <h3 class="m-0 text-xl font-semibold">
                {{
                  selectedMode
                    ? $t(`pages.servers.modes.${selectedMode.key}.name`)
                    : "—"
                }}
              </h3>
              <p class="m-0 text-sm text-white/55">
                {{ formatHostedDuration(safeDuration, t) }}
                <span v-if="displayQuote"> · {{ displayQuote.hours }}h</span>
                · {{ $t("pages.hosting.slots", { n: safeSlots }) }}
              </p>
            </div>

            <div class="relative space-y-2 border-y border-white/10 py-4">
              <div
                v-if="displayQuote"
                class="space-y-1 text-xs text-white/45"
              >
                <div class="flex justify-between gap-2">
                  <span>{{ $t("pages.servers.host.price_base") }}</span>
                  <span class="font-mono text-white/70">{{
                    formatPrice(displayQuote.base_price_irr || displayQuote.price_irr)
                  }}</span>
                </div>
                <div
                  v-if="(displayQuote.extra_slots || 0) > 0"
                  class="flex justify-between gap-2"
                >
                  <span>
                    {{
                      $t("pages.servers.host.price_extra_slots", {
                        n: displayQuote.extra_slots,
                      })
                    }}
                  </span>
                  <span class="font-mono text-white/70">{{
                    formatPrice(displayQuote.slots_price_irr || 0)
                  }}</span>
                </div>
              </div>
              <div class="flex items-end justify-between gap-3 pt-1">
                <span class="text-sm text-white/55">
                  {{ $t("pages.servers.host.price") }}
                </span>
                <span
                  v-if="quoting && !displayQuote"
                  class="text-sm text-white/40"
                >
                  …
                </span>
                <span
                  v-else-if="displayQuote"
                  class="font-mono text-2xl font-bold tabular-nums transition-all duration-300"
                  :class="{ 'opacity-70': quoting }"
                >
                  {{ formatPrice(displayQuote.price_irr) }}
                </span>
                <span v-else class="text-sm text-white/40">—</span>
              </div>
              <div
                v-if="displayQuote?.price_ypoint"
                class="flex items-center justify-end gap-1.5 font-mono text-sm text-[hsl(var(--tac-amber))]"
              >
                <img
                  src="/img/ypoint-logo.png"
                  alt=""
                  class="h-4 w-4 object-contain"
                />
                {{ displayQuote.price_ypoint }}
              </div>
              <p class="m-0 text-xs text-white/40">
                {{
                  $t("pages.servers.host.rate_hint", {
                    toman: formatTomanAmount(
                      tomanToIrr(rateToman) ||
                        overview?.price_irr_per_hour ||
                        0,
                      locale.startsWith("fa") ? "fa-IR" : "en-US",
                    ),
                    ypoint: rateYpoint || overview?.price_ypoint_per_hour || 0,
                  })
                }}
              </p>
              <p
                v-if="slotRateToman > 0"
                class="m-0 text-xs text-white/40"
              >
                {{
                  $t("pages.servers.host.slot_rate_hint", {
                    toman: slotRateToman,
                    ypoint: slotRateYpoint || 0,
                  })
                }}
              </p>
              <p
                v-if="quoteError && !(displayQuote && displayQuote.price_irr > 0)"
                class="m-0 text-xs text-red-300"
              >
                {{ quoteError }}
              </p>
            </div>

            <Button
              type="button"
              size="lg"
              class="relative mt-auto w-full"
              :disabled="!canRent"
              @click="openCheckout"
            >
              <Clock class="h-4 w-4" />
              {{ $t("pages.servers.host.rent") }}
            </Button>
          </aside>
        </div>

        <section v-if="signedIn" class="space-y-3">
          <h2 class="m-0 text-lg font-semibold">
            {{ $t("pages.servers.host.my_servers") }}
          </h2>
          <p
            v-if="!myHostServers.length"
            class="m-0 text-sm text-muted-foreground"
          >
            {{ $t("pages.servers.host.no_servers") }}
          </p>
          <div v-else class="grid gap-3 sm:grid-cols-2">
            <NuxtLink
              v-for="server in myHostServers"
              :key="server.id"
              :to="`/hosting/${server.id}`"
              class="group relative flex items-center gap-3 overflow-hidden rounded-xl border border-[hsl(var(--tac-amber)/0.28)] bg-gradient-to-r from-[hsl(var(--tac-amber)/0.1)] via-card/80 to-card/60 p-4 transition hover:border-[hsl(var(--tac-amber)/0.55)]"
            >
              <span
                class="absolute inset-y-0 start-0 w-1 bg-[hsl(var(--tac-amber))]"
                aria-hidden="true"
              />
              <div
                class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[hsl(var(--tac-amber)/0.15)] text-[hsl(var(--tac-amber))]"
              >
                <Server class="h-5 w-5" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="truncate font-semibold">{{ server.label }}</span>
                  <Badge :variant="hostedStatusVariant(server.status)">
                    {{ $t(`pages.hosting.status.${server.status}`) }}
                  </Badge>
                  <Badge v-if="server.section_mode" variant="outline">
                    {{ $t(`pages.servers.modes.${server.section_mode}.name`) }}
                  </Badge>
                </div>
                <p class="m-0 mt-1 text-xs text-muted-foreground">
                  {{
                    $t("pages.hosting.expires_at", {
                      date: formatHostedDate(server.expires_at, locale),
                    })
                  }}
                </p>
              </div>
            </NuxtLink>
          </div>
        </section>
      </div>
    </PageTransition>

    <Dialog v-model:open="checkoutOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ $t("pages.servers.host.rent") }}</DialogTitle>
          <DialogDescription>
            <span v-if="selectedMode">
              {{ $t(`pages.servers.modes.${selectedMode.key}.name`) }}
            </span>
            · {{ formatHostedDuration(duration, t) }}
            · {{ $t("pages.hosting.slots", { n: safeSlots }) }}
            <span v-if="displayQuote">
              · {{ formatPrice(displayQuote.price_irr) }}</span
            >
          </DialogDescription>
        </DialogHeader>

        <ol
          class="m-0 list-decimal space-y-1 rounded-lg border border-border bg-muted/20 py-2.5 pe-3 ps-7 text-[0.75rem] leading-relaxed text-muted-foreground"
        >
          <li>{{ $t("pages.servers.host.terms_1") }}</li>
          <li>{{ $t("pages.servers.host.terms_2") }}</li>
          <li>{{ $t("pages.hosting.terms_3") }}</li>
          <li>{{ $t("pages.store.terms_1") }}</li>
        </ol>

        <button
          type="button"
          class="mt-4 flex w-full items-start gap-3 rounded-lg border px-3 py-3 text-start"
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

        <DialogFooter class="gap-2">
          <Button
            type="button"
            variant="ghost"
            :disabled="paying"
            @click="checkoutOpen = false"
          >
            {{ $t("common.cancel") }}
          </Button>
          <Button
            v-if="displayQuote?.price_ypoint"
            type="button"
            variant="outline"
            :disabled="paying || !termsAccepted || !canAffordYpoint"
            @click="pay('ypoint')"
          >
            <img
              src="/img/ypoint-logo.png"
              alt=""
              class="h-4 w-4 object-contain"
            />
            {{
              $t("pages.store.pay_with_ypoint", {
                n: displayQuote.price_ypoint,
              })
            }}
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
      </DialogScrollContent>
    </Dialog>
  </div>
</template>
