<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import {
  ArrowLeft,
  Ban,
  Crown,
  Eye,
  ListOrdered,
  Map as MapIcon,
  RefreshCw,
  Settings2,
  Trash2,
  UserPlus,
} from "lucide-vue-next";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import QuickServerConnect from "~/components/match/QuickServerConnect.vue";
import { useAuthStore } from "~/stores/AuthStore";
import { hostedApi, hostedErrorMessage } from "~/composables/useHostedServers";
import { generateQuery } from "~/graphql/graphqlGen";
import { $ } from "~/generated/zeus";
import { e_player_roles_enum } from "~/generated/zeus";
import cleanMapName from "~/utilities/cleanMapName";
import { csRankIcon } from "~/utilities/csRank";
import { formatTomanAmount } from "~/utilities/irrToman";
import {
  tacticalCtaButtonClasses,
  tacticalSectionLabelClasses,
  tacticalSectionTickClasses,
  tacticalTabsListClasses,
  tacticalTabsTriggerClasses,
} from "~/utilities/tacticalClasses";
import { useQuery } from "@vue/apollo-composable";
import gql from "graphql-tag";
import { SERVER_MODES } from "~/utilities/serverModes";

const VIP_DURATIONS = ["1d", "7d", "30d", "90d", "perm"] as const;

type PublicRankRow = {
  steam_id: string;
  name: string | null;
  points: number;
  skill_group: number;
  rank_name: string;
};

type DetailSettings = {
  show_vips: boolean;
  show_ranks: boolean;
  show_bans: boolean;
};

type ServerDetails = {
  can_manage: boolean;
  settings: DetailSettings;
  vip_shop?: {
    packages: Array<{
      duration: "7d" | "30d" | "90d";
      price_irr?: number;
      /** @deprecated legacy API field — same Rials value */
      price_ypoint?: number;
    }>;
  };
  vips?: Array<{
    steam_id: string;
    name: string | null;
    avatar_url: string | null;
    expires_at: string | null;
  }>;
  ranks?: PublicRankRow[];
  bans?: Array<{
    steam_id: string;
    name: string | null;
    reason: string | null;
    expires_at: string | null;
  }>;
};

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const auth = useAuthStore();
const isAdmin = computed(() => auth.isAdmin);
const canManageDedicated = computed(() =>
  auth.isRoleAbove(e_player_roles_enum.moderator),
);
const serverId = computed(() => String(route.params.id || ""));

const details = ref<ServerDetails | null>(null);
const canManageVip = computed(
  () => isAdmin.value || !!details.value?.can_manage,
);
const detailsLoading = ref(true);
const detailsSettings = ref<DetailSettings>({
  show_vips: true,
  show_ranks: true,
  show_bans: true,
});
const activeTab = ref<"vips" | "ranks" | "bans">("ranks");
const vipForm = ref({ steam_id: "", duration: "30d" as (typeof VIP_DURATIONS)[number] });
const vipBusy = ref(false);
const heroReady = ref(false);
const rankQuery = ref("");
const buyDuration = ref<"7d" | "30d" | "90d" | "">("");
const buyBusy = ref(false);
const buyTerms = ref(false);
const rankBusy = ref(false);
const rankEditSteamId = ref<string | null>(null);
const rankEditPoints = ref("");
const rankAddSteamId = ref("");
const rankAddPoints = ref("");
const rankAddSkill = ref("7");

const RANK_SKILL_OPTIONS = [
  { skill: 1, name: "Silver I" },
  { skill: 2, name: "Silver II" },
  { skill: 3, name: "Silver III" },
  { skill: 4, name: "Silver IV" },
  { skill: 5, name: "Silver Elite" },
  { skill: 6, name: "Silver Elite Master" },
  { skill: 7, name: "Gold Nova I" },
  { skill: 8, name: "Gold Nova II" },
  { skill: 9, name: "Gold Nova III" },
  { skill: 10, name: "Gold Nova Master" },
  { skill: 11, name: "Master Guardian I" },
  { skill: 12, name: "Master Guardian II" },
  { skill: 13, name: "Master Guardian Elite" },
  { skill: 14, name: "Distinguished Master Guardian" },
  { skill: 15, name: "Legendary Eagle" },
  { skill: 16, name: "Legendary Eagle Master" },
  { skill: 17, name: "Supreme Master First Class" },
  { skill: 18, name: "The Global Elite" },
];

const {
  result: serverResult,
  loading: serverLoading,
} = useQuery(
  generateQuery({
    servers: [
      {
        where: { id: { _eq: $("id", "uuid!") } },
        limit: 1,
      },
      {
        id: true,
        label: true,
        type: true,
        game: true,
        region: true,
        connected: true,
        connection_link: true,
        connection_string: true,
        max_players: true,
        game_mode: {
          slug: true,
          name: true,
        },
        server_region: {
          is_lan: true,
        },
      },
    ],
  }),
  () => ({ id: serverId.value }),
  () => ({
    enabled: /^[0-9a-f-]{36}$/i.test(serverId.value),
  }),
);

const { result: liveResult } = useQuery(
  generateQuery({
    getDedicatedServerInfo: [
      {},
      { id: true, map: true, players: true, lastPing: true },
    ],
  }),
  null,
  () => ({ pollInterval: 15_000 }),
);

// The Servers section's servers have no page of their own: their mode's page
// lists and joins them. Raw document: section_mode is newer than the
// generated client.
const { result: sectionResult } = useQuery(
  gql`
    query PublicServerSectionMode($id: uuid!) {
      servers(where: { id: { _eq: $id } }, limit: 1) {
        section_mode
      }
    }
  `,
  () => ({ id: serverId.value }),
  () => ({
    enabled: /^[0-9a-f-]{36}$/i.test(serverId.value),
  }),
);

watch(
  () => sectionResult.value?.servers?.[0]?.section_mode as string | undefined,
  (mode) => {
    if (mode && SERVER_MODES.some((entry) => entry.key === mode)) {
      void navigateTo(`/game-servers/${mode}`, { replace: true });
    }
  },
  { immediate: true },
);

const server = computed(() => serverResult.value?.servers?.[0] ?? null);
const live = computed(
  () =>
    (liveResult.value?.getDedicatedServerInfo || []).find(
      (row: { id: string }) => row.id === serverId.value,
    ) ?? null,
);

const mapName = computed(() => {
  const raw = String(live.value?.map || "default");
  return raw.replace(/^workshop\//, "").split("/").pop() || "default";
});
const mapLabel = computed(() => cleanMapName(mapName.value));
const players = computed(() => Number(live.value?.players || 0));
const maxPlayers = computed(() => Number(server.value?.max_players || 0));
const capacityPercent = computed(() => {
  if (!maxPlayers.value) return 0;
  return Math.min(100, Math.round((players.value / maxPlayers.value) * 100));
});

const vips = computed(() => details.value?.vips || []);
const ranks = computed(() => details.value?.ranks || []);
const bans = computed(() => details.value?.bans || []);
const vipPackages = computed(() => details.value?.vip_shop?.packages || []);
const selectedVipPackage = computed(
  () =>
    vipPackages.value.find((p) => p.duration === buyDuration.value) || null,
);

function vipPackagePriceIrr(pkg: {
  price_irr?: number;
  price_ypoint?: number;
}) {
  const irr = Number(pkg.price_irr);
  if (Number.isFinite(irr) && irr > 0) return irr;
  const legacy = Number(pkg.price_ypoint);
  if (Number.isFinite(legacy) && legacy > 0) return legacy;
  return 0;
}

function formatVipPrice(pkg: { price_irr?: number; price_ypoint?: number }) {
  return formatTomanAmount(vipPackagePriceIrr(pkg), locale.value);
}

watch(
  vipPackages,
  (pkgs) => {
    if (!pkgs.length) {
      buyDuration.value = "";
      return;
    }
    if (!pkgs.some((p) => p.duration === buyDuration.value)) {
      buyDuration.value = pkgs[0].duration;
    }
  },
  { immediate: true },
);

const filteredRanks = computed(() => {
  const q = rankQuery.value.trim().toLowerCase();
  if (!q) return ranks.value;
  return ranks.value.filter((row) => {
    const name = (row.name || "").toLowerCase();
    return name.includes(q) || row.steam_id.includes(q) || row.rank_name.toLowerCase().includes(q);
  });
});

const podiumRanks = computed(() => filteredRanks.value.slice(0, 3));
const listRanks = computed(() => filteredRanks.value.slice(3));

function tabVisible(tab: "vips" | "ranks" | "bans"): boolean {
  if (details.value?.can_manage) return true;
  const settings = details.value?.settings || detailsSettings.value;
  if (tab === "vips") return settings.show_vips;
  if (tab === "ranks") return settings.show_ranks;
  return settings.show_bans;
}

function formatRemaining(expiresAt: string | null): string {
  if (!expiresAt) return String(t("pages.public_servers.vip_permanent"));
  const ms = new Date(expiresAt).getTime() - Date.now();
  if (ms <= 0) return "—";
  const days = Math.floor(ms / 86_400_000);
  const hours = Math.floor((ms % 86_400_000) / 3_600_000);
  if (days >= 1) {
    return String(
      t("pages.public_servers.vip_remaining_days", { days, hours }),
    );
  }
  const mins = Math.floor((ms % 3_600_000) / 60_000);
  if (hours >= 1) {
    return String(
      t("pages.public_servers.vip_remaining_hours", { hours, mins }),
    );
  }
  return String(t("pages.public_servers.vip_remaining_mins", { mins }));
}

async function loadDetails() {
  if (!/^[0-9a-f-]{36}$/i.test(serverId.value)) {
    detailsLoading.value = false;
    return;
  }
  detailsLoading.value = true;
  try {
    const data = await hostedApi<ServerDetails>(
      `/hosted-servers/public-details/${serverId.value}`,
    );
    details.value = data;
    detailsSettings.value = { ...data.settings };
    if (!tabVisible(activeTab.value)) {
      const first = (["vips", "ranks", "bans"] as const).find((tab) =>
        tabVisible(tab),
      );
      if (first) activeTab.value = first;
    }
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
    details.value = null;
  } finally {
    detailsLoading.value = false;
  }
}

function beginRankEdit(row: PublicRankRow) {
  rankEditSteamId.value = row.steam_id;
  rankEditPoints.value = String(row.points);
}

function cancelRankEdit() {
  rankEditSteamId.value = null;
  rankEditPoints.value = "";
}

async function saveRankEdit(row: PublicRankRow) {
  if (!details.value?.can_manage || rankBusy.value) return;
  const points = Math.round(Number(rankEditPoints.value));
  if (!Number.isFinite(points) || points < 0) {
    toast({
      variant: "destructive",
      title: t("pages.public_servers.details.rank_points_invalid"),
    });
    return;
  }
  rankBusy.value = true;
  try {
    await hostedApi(`/hosted-servers/public-details/${serverId.value}/ranks/set`, {
      method: "POST",
      body: { steam_id: row.steam_id, points, name: row.name || undefined },
    });
    toast({ title: t("pages.public_servers.details.rank_saved") });
    cancelRankEdit();
    await loadDetails();
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    rankBusy.value = false;
  }
}

async function setRankBySkill(row: PublicRankRow, skillGroup: number) {
  if (!details.value?.can_manage || rankBusy.value) return;
  rankBusy.value = true;
  try {
    await hostedApi(`/hosted-servers/public-details/${serverId.value}/ranks/set`, {
      method: "POST",
      body: {
        steam_id: row.steam_id,
        skill_group: skillGroup,
        name: row.name || undefined,
      },
    });
    toast({ title: t("pages.public_servers.details.rank_saved") });
    await loadDetails();
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    rankBusy.value = false;
  }
}

async function addOrSetRank() {
  if (!details.value?.can_manage || rankBusy.value) return;
  const steamId = rankAddSteamId.value.trim();
  if (!/\b7656119\d{10}\b/.test(steamId)) {
    toast({
      variant: "destructive",
      title: t("pages.public_servers.details.rank_steam_invalid"),
    });
    return;
  }
  const pointsRaw = rankAddPoints.value.trim();
  const body: Record<string, unknown> = { steam_id: steamId };
  if (pointsRaw !== "") {
    const points = Math.round(Number(pointsRaw));
    if (!Number.isFinite(points) || points < 0) {
      toast({
        variant: "destructive",
        title: t("pages.public_servers.details.rank_points_invalid"),
      });
      return;
    }
    body.points = points;
  } else {
    body.skill_group = Number(rankAddSkill.value) || 7;
  }
  rankBusy.value = true;
  try {
    await hostedApi(`/hosted-servers/public-details/${serverId.value}/ranks/set`, {
      method: "POST",
      body,
    });
    toast({ title: t("pages.public_servers.details.rank_saved") });
    rankAddSteamId.value = "";
    rankAddPoints.value = "";
    await loadDetails();
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    rankBusy.value = false;
  }
}

async function setVisibility(key: "vips" | "ranks" | "bans", value: boolean) {
  if (!details.value?.can_manage) return;
  const field =
    key === "vips" ? "show_vips" : key === "ranks" ? "show_ranks" : "show_bans";
  const previous = { ...detailsSettings.value };
  detailsSettings.value = { ...detailsSettings.value, [field]: value };
  try {
    const result = await hostedApi<{ settings: DetailSettings }>(
      `/hosted-servers/public-details/${serverId.value}/settings`,
      { method: "POST", body: { [field]: value } },
    );
    detailsSettings.value = { ...result.settings };
    if (details.value) details.value.settings = { ...result.settings };
    await loadDetails();
  } catch (error) {
    detailsSettings.value = previous;
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  }
}

async function runVipAction(
  action: (id: string) => Promise<string | null>,
): Promise<void> {
  if (!serverId.value || vipBusy.value) return;
  vipBusy.value = true;
  try {
    const title = await action(serverId.value);
    await loadDetails();
    if (title) toast({ title });
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    vipBusy.value = false;
  }
}

function grantVip() {
  return runVipAction(async (id) => {
    const result = await hostedApi<{ listed: boolean }>(
      `/hosted-servers/vip/${id}/grant`,
      {
        method: "POST",
        body: {
          steam_id: vipForm.value.steam_id.trim(),
          duration: vipForm.value.duration,
        },
      },
    );
    vipForm.value.steam_id = "";
    return String(
      t(
        result.listed
          ? "pages.public_servers.vip_admin.added"
          : "pages.public_servers.vip_admin.added_unlisted",
      ),
    );
  });
}

function revokeVip(steamId: string) {
  if (
    !window.confirm(String(t("pages.public_servers.vip_admin.remove_confirm")))
  ) {
    return;
  }
  return runVipAction(async (id) => {
    await hostedApi(`/hosted-servers/vip/${id}/revoke`, {
      method: "POST",
      body: { steam_id: steamId },
    });
    return String(t("pages.public_servers.vip_admin.removed"));
  });
}

function syncVips() {
  return runVipAction(async (id) => {
    const result = await hostedApi<{
      imported: number;
      removed: number;
      unregistered: number;
    }>(`/hosted-servers/vip/${id}/sync`, { method: "POST" });
    return String(
      t("pages.public_servers.vip_admin.synced", {
        imported: result.imported,
        removed: result.removed,
        unregistered: result.unregistered,
      }),
    );
  });
}

function onHeroError(e: Event) {
  const img = e.target as HTMLImageElement;
  img.src = "/img/maps/screenshots/default.webp";
}

watch(serverId, () => {
  void loadDetails();
});

onMounted(() => {
  void loadDetails();
  requestAnimationFrame(() => {
    heroReady.value = true;
  });
});

async function buyVip() {
  if (
    !serverId.value ||
    !selectedVipPackage.value ||
    buyBusy.value ||
    !buyTerms.value
  ) {
    return;
  }
  if (!auth.me?.steam_id) {
    toast({
      variant: "destructive",
      title: t("pages.store.sign_in_required"),
    });
    return;
  }
  buyBusy.value = true;
  try {
    const result = await hostedApi<{
      paid?: boolean;
      deepLink?: string;
      orderId?: string;
    }>("/hosted-servers/vip-checkout", {
      method: "POST",
      body: {
        server_id: serverId.value,
        duration: selectedVipPackage.value.duration,
        termsAccepted: true,
      },
    });
    buyTerms.value = false;
    if (result?.deepLink) {
      toast({
        title: t("pages.store.checkout_started"),
        description: t("pages.store.checkout_hint"),
      });
      window.open(result.deepLink, "_blank", "noopener,noreferrer");
    } else {
      toast({ title: t("pages.public_servers.vip_shop.bought") });
      await loadDetails();
    }
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    buyBusy.value = false;
  }
}
</script>

<template>
  <div class="pb-16">
    <PageTransition :delay="0">
      <div class="mb-4">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          class="-ms-2 gap-1.5 text-muted-foreground"
          @click="router.push('/public-servers')"
        >
          <ArrowLeft class="h-4 w-4" />
          {{ $t("pages.public_servers.title") }}
        </Button>
      </div>
    </PageTransition>

    <!-- Hero -->
    <PageTransition :delay="40">
      <section class="relative isolate -mx-1 overflow-hidden sm:mx-0">
        <div
          class="relative overflow-hidden rounded-none border-y border-border/60 sm:rounded-2xl sm:border"
        >
          <div class="absolute inset-0">
            <img
              :src="`/img/maps/screenshots/${mapName}.webp`"
              :alt="mapLabel"
              class="h-full w-full object-cover transition-transform duration-[1.6s] ease-out"
              :class="heroReady ? 'scale-100' : 'scale-[1.06]'"
              @error="onHeroError"
            />
            <div
              class="absolute inset-0 bg-gradient-to-t from-background via-background/75 to-background/20"
            />
            <div
              class="absolute inset-0 bg-[radial-gradient(ellipse_80%_60%_at_80%_0%,hsl(var(--tac-amber)/0.22),transparent_60%)]"
            />
            <div
              class="pointer-events-none absolute inset-x-0 bottom-0 h-24 bg-gradient-to-t from-background to-transparent"
            />
          </div>

          <div
            class="relative z-10 flex min-h-[min(58vh,32rem)] flex-col justify-end gap-8 p-5 pb-8 sm:p-8 sm:pb-10 lg:p-12"
          >
            <div class="max-w-3xl space-y-4">
              <p
                class="m-0 inline-flex items-center gap-2 font-mono text-[0.68rem] font-semibold uppercase tracking-[0.22em] text-[hsl(var(--tac-amber))]"
              >
                <span class="inline-block h-px w-6 bg-[hsl(var(--tac-amber))]" />
                {{ $t("pages.public_servers.details.page_title") }}
              </p>

              <div class="flex flex-wrap items-center gap-2">
                <Badge
                  v-if="server?.type"
                  variant="secondary"
                  class="font-mono text-[0.65rem] uppercase tracking-[0.14em]"
                >
                  {{ server.type }}
                </Badge>
                <Badge
                  v-if="server?.game_mode?.name"
                  variant="outline"
                  class="border-white/20 bg-black/25 text-foreground/90 backdrop-blur-sm"
                >
                  {{ server.game_mode.name }}
                </Badge>
                <Badge
                  v-if="server?.region"
                  variant="outline"
                  class="border-white/15 bg-black/20 text-muted-foreground backdrop-blur-sm"
                >
                  {{ server.region }}
                </Badge>
              </div>

              <h1
                class="m-0 font-sans text-[clamp(2rem,6vw,3.75rem)] font-bold leading-[0.98] tracking-tight text-foreground drop-shadow-[0_2px_24px_rgba(0,0,0,0.45)]"
              >
                <Skeleton
                  v-if="serverLoading && !server"
                  class="h-12 w-72 max-w-full"
                />
                <template v-else>
                  {{
                    server?.label ||
                    $t("pages.public_servers.details.page_fallback")
                  }}
                </template>
              </h1>

              <p
                class="m-0 max-w-xl text-sm leading-relaxed text-muted-foreground sm:text-[0.95rem]"
              >
                {{ $t("pages.public_servers.details.page_blurb") }}
              </p>
            </div>

            <div
              class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"
            >
              <div class="flex flex-wrap items-center gap-2">
                <div
                  v-if="server"
                  class="min-w-[12rem] flex-1 sm:flex-none [&>div]:w-full sm:[&>div]:w-auto [&_a]:w-full sm:[&_a]:w-auto [&_a_button]:w-full sm:[&_a_button]:w-auto"
                >
                  <QuickServerConnect :server="server" highlight />
                </div>
                <Button
                  v-if="canManageDedicated && server"
                  as-child
                  variant="outline"
                  class="border-border/80 bg-background/45 backdrop-blur-sm"
                >
                  <NuxtLink :to="`/dedicated-servers/${server.id}`">
                    <Settings2 class="me-1.5 h-4 w-4" />
                    {{ $t("pages.public_servers.manage") }}
                  </NuxtLink>
                </Button>
              </div>

              <div
                class="grid grid-cols-3 gap-2 sm:min-w-[18rem]"
              >
                <div
                  class="rounded-lg border border-border/50 bg-background/55 px-3 py-2.5 backdrop-blur-md"
                >
                  <div
                    class="font-mono text-[0.6rem] uppercase tracking-[0.16em] text-muted-foreground"
                  >
                    {{ $t("pages.public_servers.players") }}
                  </div>
                  <div class="mt-1 font-mono text-lg font-semibold tabular-nums">
                    {{ players }}
                    <span class="text-sm text-muted-foreground"
                      >/{{ maxPlayers || "—" }}</span
                    >
                  </div>
                </div>
                <div
                  class="rounded-lg border border-border/50 bg-background/55 px-3 py-2.5 backdrop-blur-md"
                >
                  <div
                    class="flex items-center gap-1 font-mono text-[0.6rem] uppercase tracking-[0.16em] text-muted-foreground"
                  >
                    <MapIcon class="h-3 w-3 text-[hsl(var(--tac-amber))]" />
                    Map
                  </div>
                  <div class="mt-1 truncate text-sm font-semibold">
                    {{ mapLabel }}
                  </div>
                </div>
                <div
                  class="rounded-lg border border-border/50 bg-background/55 px-3 py-2.5 backdrop-blur-md"
                >
                  <div
                    class="font-mono text-[0.6rem] uppercase tracking-[0.16em] text-muted-foreground"
                  >
                    Fill
                  </div>
                  <div class="mt-1.5 h-2 overflow-hidden rounded-full bg-primary/15">
                    <div
                      class="h-full rounded-full bg-[hsl(var(--tac-amber))] transition-all duration-500"
                      :style="{ width: `${capacityPercent}%` }"
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </PageTransition>

    <!-- Body -->
    <div class="mt-8 grid gap-6 lg:grid-cols-[minmax(0,1fr)_17rem]">
      <PageTransition :delay="90">
        <div class="min-w-0 space-y-5">
          <div
            v-if="detailsLoading"
            class="space-y-3 rounded-xl border border-border/70 p-5"
          >
            <Skeleton class="h-8 w-48" />
            <Skeleton class="h-14 w-full" />
            <Skeleton class="h-14 w-full" />
            <Skeleton class="h-14 w-full" />
          </div>

          <template v-else>
            <Tabs v-model="activeTab" :scroll-floor="false">
              <TabsList :class="tacticalTabsListClasses" class="w-full flex-wrap sm:w-auto">
                <TabsTrigger
                  v-for="tab in (['ranks', 'vips', 'bans'] as const)"
                  :key="tab"
                  :value="tab"
                  :disabled="!tabVisible(tab)"
                  :class="tacticalTabsTriggerClasses"
                >
                  {{ $t(`pages.public_servers.details.tabs.${tab}`) }}
                  <span
                    class="ms-1.5 font-mono text-[0.65rem] text-[hsl(var(--tac-amber))] opacity-80"
                  >
                    {{
                      tab === "ranks"
                        ? ranks.length
                        : tab === "vips"
                          ? vips.length
                          : bans.length
                    }}
                  </span>
                </TabsTrigger>
              </TabsList>

              <TabsContent value="vips" class="mt-5 outline-none">
                <template v-if="tabVisible('vips')">
                  <div
                    class="mb-4 flex items-center gap-2"
                    :class="tacticalSectionLabelClasses"
                  >
                    <span :class="tacticalSectionTickClasses" />
                    {{ $t("pages.public_servers.details.show_vips") }}
                    <span class="font-mono text-[hsl(var(--tac-amber))]"
                      >{{ vips.length }}</span
                    >
                  </div>

                  <ul v-if="vips.length" class="space-y-2">
                    <li
                      v-for="(vip, index) in vips"
                      :key="vip.steam_id"
                      class="group flex items-center gap-3 rounded-xl border border-border/60 bg-card/40 px-3.5 py-3 transition-colors hover:border-[hsl(var(--tac-amber)/0.35)] hover:bg-card/70"
                      :style="{ animationDelay: `${index * 40}ms` }"
                    >
                      <img
                        v-if="vip.avatar_url"
                        :src="vip.avatar_url"
                        alt=""
                        class="h-10 w-10 rounded-md object-cover ring-1 ring-border/60"
                      />
                      <div
                        v-else
                        class="flex h-10 w-10 items-center justify-center rounded-md bg-[hsl(var(--tac-amber)/0.12)] text-[hsl(var(--tac-amber))]"
                      >
                        <Crown class="h-5 w-5" />
                      </div>
                      <div class="min-w-0 flex-1">
                        <div class="truncate font-medium">
                          {{ vip.name || vip.steam_id }}
                        </div>
                        <div
                          class="font-mono text-[0.7rem] text-[hsl(var(--tac-amber))]"
                        >
                          {{ formatRemaining(vip.expires_at) }}
                        </div>
                      </div>
                      <Button
                        v-if="canManageVip"
                        size="icon"
                        variant="ghost"
                        class="h-8 w-8 shrink-0 opacity-70 group-hover:opacity-100"
                        :disabled="vipBusy"
                        @click="revokeVip(vip.steam_id)"
                      >
                        <Trash2 class="h-3.5 w-3.5" />
                      </Button>
                    </li>
                  </ul>
                  <p
                    v-else
                    class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                  >
                    {{ $t("pages.public_servers.vip_none") }}
                  </p>

                  <div
                    v-if="canManageVip"
                    class="mt-5 space-y-3 rounded-xl border border-border/70 bg-muted/20 p-4"
                  >
                    <p
                      class="m-0"
                      :class="tacticalSectionLabelClasses"
                    >
                      <span :class="tacticalSectionTickClasses" />
                      {{ $t("pages.public_servers.vip_admin.add") }}
                    </p>
                    <form
                      class="flex flex-col gap-2 sm:flex-row"
                      @submit.prevent="grantVip"
                    >
                      <Input
                        v-model="vipForm.steam_id"
                        dir="ltr"
                        class="min-w-0 flex-1 font-mono"
                        maxlength="120"
                        :placeholder="
                          $t('pages.public_servers.vip_admin.placeholder')
                        "
                      />
                      <Select v-model="vipForm.duration">
                        <SelectTrigger class="w-full sm:w-32 shrink-0">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem
                            v-for="duration in VIP_DURATIONS"
                            :key="duration"
                            :value="duration"
                          >
                            {{
                              $t(
                                `pages.public_servers.vip_admin.durations.${duration}`,
                              )
                            }}
                          </SelectItem>
                        </SelectContent>
                      </Select>
                      <Button
                        type="submit"
                        :class="tacticalCtaButtonClasses"
                        class="!py-2.5 shrink-0"
                        :disabled="vipBusy || !vipForm.steam_id.trim()"
                      >
                        <UserPlus class="h-3.5 w-3.5" />
                        {{ $t("pages.public_servers.vip_admin.add") }}
                      </Button>
                    </form>
                    <div class="flex flex-wrap items-center justify-between gap-2">
                      <p class="m-0 text-xs text-muted-foreground">
                        {{ $t("pages.public_servers.vip_admin.hint") }}
                      </p>
                      <Button
                        size="sm"
                        variant="outline"
                        :disabled="vipBusy"
                        @click="syncVips"
                      >
                        <RefreshCw class="me-1.5 h-3.5 w-3.5" />
                        {{ $t("pages.public_servers.vip_admin.sync") }}
                      </Button>
                    </div>
                  </div>
                </template>
                <p
                  v-else
                  class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                >
                  {{ $t("pages.public_servers.details.section_hidden") }}
                </p>
              </TabsContent>

              <TabsContent value="ranks" class="mt-5 outline-none">
                <template v-if="tabVisible('ranks')">
                  <div
                    class="mb-2 flex flex-wrap items-end justify-between gap-3"
                  >
                    <div>
                      <div
                        class="flex items-center gap-2"
                        :class="tacticalSectionLabelClasses"
                      >
                        <span :class="tacticalSectionTickClasses" />
                        {{ $t("pages.public_servers.details.show_ranks") }}
                      </div>
                      <p class="m-0 text-xs text-muted-foreground">
                        {{ $t("pages.public_servers.details.ranks_hint") }}
                      </p>
                    </div>
                    <Input
                      v-if="ranks.length > 6"
                      v-model="rankQuery"
                      dir="auto"
                      class="h-9 w-full max-w-xs"
                      :placeholder="
                        $t('pages.public_servers.details.ranks_search')
                      "
                    />
                  </div>

                  <div
                    v-if="details?.can_manage"
                    class="mb-4 space-y-2 rounded-xl border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.06)] p-3"
                  >
                    <p class="text-xs font-medium text-foreground">
                      {{ $t("pages.public_servers.details.rank_set_title") }}
                    </p>
                    <div class="flex flex-wrap items-center gap-2">
                      <Input
                        v-model="rankAddSteamId"
                        class="h-9 min-w-[12rem] flex-1 font-mono text-xs"
                        :placeholder="
                          $t('pages.public_servers.details.rank_steam_placeholder')
                        "
                      />
                      <Input
                        v-model="rankAddPoints"
                        type="number"
                        min="0"
                        class="h-9 w-28 font-mono text-xs"
                        :placeholder="
                          $t('pages.public_servers.details.rank_points_placeholder')
                        "
                      />
                      <Select v-model="rankAddSkill">
                        <SelectTrigger class="h-9 w-44">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem
                            v-for="opt in RANK_SKILL_OPTIONS"
                            :key="opt.skill"
                            :value="String(opt.skill)"
                          >
                            {{ opt.name }}
                          </SelectItem>
                        </SelectContent>
                      </Select>
                      <Button
                        type="button"
                        size="sm"
                        variant="tactical"
                        :disabled="rankBusy"
                        @click="addOrSetRank"
                      >
                        {{ $t("pages.public_servers.details.rank_set_button") }}
                      </Button>
                    </div>
                    <p class="text-[0.7rem] text-muted-foreground">
                      {{ $t("pages.public_servers.details.rank_set_hint") }}
                    </p>
                  </div>

                  <template v-if="filteredRanks.length">
                    <div
                      v-if="podiumRanks.length && !rankQuery.trim()"
                      class="mb-4 grid gap-2 sm:grid-cols-3"
                    >
                      <div
                        v-for="(row, index) in podiumRanks"
                        :key="row.steam_id"
                        class="relative overflow-hidden rounded-xl border px-4 py-4"
                        :class="
                          index === 0
                            ? 'border-[hsl(var(--tac-amber)/0.55)] bg-[hsl(var(--tac-amber)/0.1)] sm:order-2 sm:-mt-1'
                            : index === 1
                              ? 'border-border/70 bg-card/50 sm:order-1'
                              : 'border-border/70 bg-card/50 sm:order-3'
                        "
                      >
                        <div
                          class="mb-3 flex items-center justify-between gap-2"
                        >
                          <span
                            class="font-mono text-xs uppercase tracking-[0.16em] text-muted-foreground"
                            >#{{ index + 1 }}</span
                          >
                          <img
                            v-if="csRankIcon(7, row.skill_group)"
                            :src="csRankIcon(7, row.skill_group)!"
                            :alt="row.rank_name"
                            class="h-9 w-9"
                          />
                        </div>
                        <div class="truncate text-base font-semibold">
                          {{ row.name || row.steam_id }}
                        </div>
                        <div class="mt-0.5 text-xs text-muted-foreground">
                          {{ row.rank_name }}
                        </div>
                        <div
                          class="mt-3 font-mono text-lg text-[hsl(var(--tac-amber))]"
                        >
                          {{ row.points }}
                          <span class="text-xs text-muted-foreground">pts</span>
                        </div>
                      </div>
                    </div>

                    <ul class="space-y-1.5">
                      <li
                        v-for="(row, index) in rankQuery.trim()
                          ? filteredRanks
                          : listRanks"
                        :key="row.steam_id"
                        class="flex items-center gap-3 rounded-xl border border-border/50 bg-card/30 px-3.5 py-2.5 transition-colors hover:border-[hsl(var(--tac-amber)/0.35)] hover:bg-card/60"
                      >
                        <span
                          class="w-8 shrink-0 font-mono text-sm text-muted-foreground tabular-nums"
                          >#{{
                            rankQuery.trim() ? index + 1 : index + 4
                          }}</span
                        >
                        <img
                          v-if="csRankIcon(7, row.skill_group)"
                          :src="csRankIcon(7, row.skill_group)!"
                          :alt="row.rank_name"
                          class="h-7 w-7 shrink-0"
                        />
                        <ListOrdered
                          v-else
                          class="h-5 w-5 shrink-0 text-muted-foreground"
                        />
                        <div class="min-w-0 flex-1">
                          <div class="truncate font-medium">
                            {{ row.name || row.steam_id }}
                          </div>
                          <div class="text-xs text-muted-foreground">
                            {{ row.rank_name }}
                          </div>
                        </div>
                        <template
                          v-if="
                            details?.can_manage &&
                            rankEditSteamId === row.steam_id
                          "
                        >
                          <Input
                            v-model="rankEditPoints"
                            type="number"
                            min="0"
                            class="h-8 w-24 font-mono text-xs"
                          />
                          <Button
                            type="button"
                            size="sm"
                            variant="tactical"
                            :disabled="rankBusy"
                            @click="saveRankEdit(row)"
                          >
                            {{ $t("common.save") }}
                          </Button>
                          <Button
                            type="button"
                            size="sm"
                            variant="ghost"
                            :disabled="rankBusy"
                            @click="cancelRankEdit"
                          >
                            {{ $t("common.cancel") }}
                          </Button>
                        </template>
                        <template v-else>
                          <span
                            class="shrink-0 font-mono text-sm text-[hsl(var(--tac-amber))]"
                            >{{ row.points }}</span
                          >
                          <template v-if="details?.can_manage">
                            <Select
                              :model-value="String(row.skill_group)"
                              @update:model-value="
                                (v) => setRankBySkill(row, Number(v))
                              "
                            >
                              <SelectTrigger class="h-8 w-36 shrink-0 text-xs">
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem
                                  v-for="opt in RANK_SKILL_OPTIONS"
                                  :key="opt.skill"
                                  :value="String(opt.skill)"
                                >
                                  {{ opt.name }}
                                </SelectItem>
                              </SelectContent>
                            </Select>
                            <Button
                              type="button"
                              size="sm"
                              variant="ghost"
                              class="shrink-0 px-2"
                              :disabled="rankBusy"
                              @click="beginRankEdit(row)"
                            >
                              {{ $t("pages.public_servers.details.rank_edit") }}
                            </Button>
                          </template>
                        </template>
                      </li>
                    </ul>
                  </template>
                  <p
                    v-else
                    class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                  >
                    {{
                      rankQuery.trim()
                        ? $t("pages.public_servers.details.ranks_no_match")
                        : $t("pages.public_servers.details.ranks_empty")
                    }}
                  </p>
                </template>
                <p
                  v-else
                  class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                >
                  {{ $t("pages.public_servers.details.section_hidden") }}
                </p>
              </TabsContent>

              <TabsContent value="bans" class="mt-5 outline-none">
                <template v-if="tabVisible('bans')">
                  <div
                    class="mb-4 flex items-center gap-2"
                    :class="tacticalSectionLabelClasses"
                  >
                    <span :class="tacticalSectionTickClasses" />
                    {{ $t("pages.public_servers.details.show_bans") }}
                    <span class="font-mono text-[hsl(var(--tac-amber))]"
                      >{{ bans.length }}</span
                    >
                  </div>

                  <ul v-if="bans.length" class="space-y-2">
                    <li
                      v-for="ban in bans"
                      :key="ban.steam_id"
                      class="flex items-start gap-3 rounded-xl border border-border/60 bg-card/40 px-3.5 py-3"
                    >
                      <div
                        class="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-destructive/10 text-destructive"
                      >
                        <Ban class="h-4 w-4" />
                      </div>
                      <div class="min-w-0 flex-1">
                        <div class="truncate font-medium">
                          {{ ban.name || ban.steam_id }}
                        </div>
                        <div class="text-xs text-muted-foreground">
                          {{
                            ban.reason ||
                            $t("pages.public_servers.details.no_reason")
                          }}
                          <span v-if="ban.expires_at">
                            · {{ formatRemaining(ban.expires_at) }}
                          </span>
                          <span v-else>
                            · {{ $t("pages.public_servers.details.perm") }}
                          </span>
                        </div>
                      </div>
                    </li>
                  </ul>
                  <p
                    v-else
                    class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                  >
                    {{ $t("pages.public_servers.details.bans_empty") }}
                  </p>
                </template>
                <p
                  v-else
                  class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                >
                  {{ $t("pages.public_servers.details.section_hidden") }}
                </p>
              </TabsContent>
            </Tabs>
          </template>
        </div>
      </PageTransition>

      <PageTransition :delay="120">
        <aside class="space-y-4 lg:sticky lg:top-20 lg:self-start">
          <div
            v-if="vipPackages.length"
            class="rounded-xl border border-border/70 bg-card/50 p-4 backdrop-blur-sm"
          >
            <div
              class="mb-3 flex items-center gap-2"
              :class="tacticalSectionLabelClasses"
            >
              <span :class="tacticalSectionTickClasses" />
              <Crown class="h-3.5 w-3.5" />
              {{ $t("pages.public_servers.vip_shop.title") }}
            </div>
            <p class="m-0 mb-3 text-xs text-muted-foreground">
              {{ $t("pages.public_servers.vip_shop.description") }}
            </p>
            <div class="space-y-2">
              <button
                v-for="pkg in vipPackages"
                :key="pkg.duration"
                type="button"
                class="flex w-full items-center justify-between rounded-lg border px-3 py-2.5 text-sm transition-colors"
                :class="
                  buyDuration === pkg.duration
                    ? 'border-[hsl(var(--tac-amber)/0.55)] bg-[hsl(var(--tac-amber)/0.08)]'
                    : 'border-border/50 hover:bg-muted/40'
                "
                @click="buyDuration = pkg.duration"
              >
                <span>{{
                  $t(`pages.public_servers.vip_shop.duration_${pkg.duration}`)
                }}</span>
                <span
                  class="font-mono text-[hsl(var(--tac-amber))] tabular-nums"
                >
                  {{ formatVipPrice(pkg) }}
                  {{ $t("pages.settings.application.finance.toman") }}
                </span>
              </button>
            </div>
            <label
              class="mt-3 flex cursor-pointer items-start gap-2 text-xs text-muted-foreground"
            >
              <Checkbox
                :model-value="buyTerms"
                class="mt-0.5"
                @update:model-value="(v) => (buyTerms = !!v)"
              />
              {{ $t("pages.public_servers.vip_shop.terms") }}
            </label>
            <Button
              class="mt-3 w-full"
              size="sm"
              :disabled="buyBusy || !buyTerms || !selectedVipPackage"
              @click="buyVip"
            >
              {{
                !auth.me?.steam_id
                  ? $t("pages.store.sign_in_required")
                  : $t("pages.public_servers.vip_shop.buy")
              }}
            </Button>
          </div>

          <div
            v-if="details?.can_manage"
            class="rounded-xl border border-border/70 bg-card/50 p-4 backdrop-blur-sm"
          >
            <div
              class="mb-3 flex items-center gap-2"
              :class="tacticalSectionLabelClasses"
            >
              <span :class="tacticalSectionTickClasses" />
              <Eye class="h-3.5 w-3.5" />
              {{ $t("pages.public_servers.details.visibility") }}
            </div>
            <div class="space-y-2.5">
              <label
                v-for="key in (['vips', 'ranks', 'bans'] as const)"
                :key="key"
                class="flex cursor-pointer items-center gap-2.5 rounded-lg border border-border/50 px-3 py-2.5 text-sm transition-colors hover:bg-muted/40"
              >
                <Checkbox
                  :model-value="detailsSettings[`show_${key}`]"
                  @update:model-value="(v) => setVisibility(key, !!v)"
                />
                {{ $t(`pages.public_servers.details.show_${key}`) }}
              </label>
            </div>
          </div>

          <div
            class="rounded-xl border border-border/70 bg-card/40 p-4 text-sm text-muted-foreground"
          >
            <p class="m-0 mb-2 font-medium text-foreground">
              {{ $t("pages.public_servers.details.page_title") }}
            </p>
            <p class="m-0 text-xs leading-relaxed">
              {{ $t("pages.public_servers.details.page_blurb") }}
            </p>
          </div>
        </aside>
      </PageTransition>
    </div>
  </div>
</template>
