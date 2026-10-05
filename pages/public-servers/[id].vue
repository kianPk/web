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
  Users,
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
import {
  tacticalCtaButtonClasses,
  tacticalSectionLabelClasses,
  tacticalSectionTickClasses,
  tacticalTabsListClasses,
  tacticalTabsTriggerClasses,
} from "~/utilities/tacticalClasses";
import { useQuery } from "@vue/apollo-composable";

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

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const isAdmin = computed(() => useAuthStore().isAdmin);
const canManageDedicated = computed(() =>
  useAuthStore().isRoleAbove(e_player_roles_enum.moderator),
);

const serverId = computed(() => String(route.params.id || ""));

const details = ref<ServerDetails | null>(null);
const detailsLoading = ref(true);
const detailsSettings = ref<DetailSettings>({
  show_vips: true,
  show_ranks: true,
  show_bans: true,
});
const activeTab = ref<"vips" | "ranks" | "bans">("vips");
const vipForm = ref({ steam_id: "", duration: "30d" as (typeof VIP_DURATIONS)[number] });
const vipBusy = ref(false);
const heroReady = ref(false);

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
      <section
        class="relative isolate overflow-hidden rounded-2xl border border-border/70"
      >
        <div class="absolute inset-0">
          <img
            :src="`/img/maps/screenshots/${mapName}.webp`"
            :alt="mapLabel"
            class="h-full w-full object-cover transition-transform duration-[1.4s] ease-out"
            :class="heroReady ? 'scale-100' : 'scale-105'"
            @error="onHeroError"
          />
          <div
            class="absolute inset-0 bg-gradient-to-t from-background via-background/80 to-background/25"
          />
          <div
            class="absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,hsl(var(--tac-amber)/0.18),transparent_55%)]"
          />
        </div>

        <div
          class="relative z-10 flex min-h-[min(52vh,28rem)] flex-col justify-end gap-6 p-5 sm:p-8 lg:p-10"
        >
          <div class="space-y-3 max-w-3xl">
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
                class="border-white/20 bg-black/20 text-foreground/90 backdrop-blur-sm"
              >
                {{ server.game_mode.name }}
              </Badge>
              <Badge
                v-if="server?.region"
                variant="outline"
                class="border-white/15 bg-black/15 text-muted-foreground backdrop-blur-sm"
              >
                {{ server.region }}
              </Badge>
            </div>

            <h1
              class="m-0 font-sans text-[clamp(1.75rem,5vw,3.25rem)] font-bold leading-[1.05] tracking-tight text-foreground"
            >
              <Skeleton
                v-if="serverLoading && !server"
                class="h-10 w-64 max-w-full"
              />
              <template v-else>
                {{
                  server?.label ||
                  $t("pages.public_servers.details.page_fallback")
                }}
              </template>
            </h1>

            <p
              class="m-0 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground"
            >
              <span class="inline-flex items-center gap-1.5">
                <MapIcon class="h-3.5 w-3.5 text-[hsl(var(--tac-amber))]" />
                {{ mapLabel }}
              </span>
              <span class="inline-flex items-center gap-1.5 font-mono">
                <Users class="h-3.5 w-3.5 text-[hsl(var(--tac-amber))]" />
                {{ players }}
                <span class="text-muted-foreground/70"
                  >/ {{ maxPlayers || "—" }}</span
                >
              </span>
            </p>

            <div
              class="h-1.5 w-full max-w-md overflow-hidden rounded-full bg-primary/15"
            >
              <div
                class="h-full rounded-full bg-[hsl(var(--tac-amber))] transition-all duration-500"
                :style="{ width: `${capacityPercent}%` }"
              />
            </div>
          </div>

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
              class="border-border/80 bg-background/40 backdrop-blur-sm"
            >
              <NuxtLink :to="`/dedicated-servers/${server.id}`">
                <Settings2 class="me-1.5 h-4 w-4" />
                {{ $t("pages.public_servers.manage") }}
              </NuxtLink>
            </Button>
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
              <TabsList :class="tacticalTabsListClasses" class="w-full sm:w-auto">
                <TabsTrigger
                  v-for="tab in (['vips', 'ranks', 'bans'] as const)"
                  :key="tab"
                  :value="tab"
                  :disabled="!tabVisible(tab)"
                  :class="tacticalTabsTriggerClasses"
                >
                  {{ $t(`pages.public_servers.details.tabs.${tab}`) }}
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
                        v-if="isAdmin"
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
                    v-if="isAdmin"
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
                    class="mb-4 flex items-center gap-2"
                    :class="tacticalSectionLabelClasses"
                  >
                    <span :class="tacticalSectionTickClasses" />
                    {{ $t("pages.public_servers.details.show_ranks") }}
                    <span class="font-mono text-[hsl(var(--tac-amber))]"
                      >{{ ranks.length }}</span
                    >
                  </div>

                  <ul v-if="ranks.length" class="space-y-2">
                    <li
                      v-for="(row, index) in ranks"
                      :key="row.steam_id"
                      class="flex items-center gap-3 rounded-xl border border-border/60 bg-card/40 px-3.5 py-3 transition-colors hover:border-[hsl(var(--tac-amber)/0.35)] hover:bg-card/70"
                    >
                      <span
                        class="w-8 shrink-0 font-mono text-sm text-muted-foreground tabular-nums"
                        >#{{ index + 1 }}</span
                      >
                      <img
                        v-if="csRankIcon(7, row.skill_group)"
                        :src="csRankIcon(7, row.skill_group)!"
                        :alt="row.rank_name"
                        class="h-8 w-8 shrink-0"
                      />
                      <ListOrdered
                        v-else
                        class="h-6 w-6 shrink-0 text-muted-foreground"
                      />
                      <div class="min-w-0 flex-1">
                        <div class="truncate font-medium">
                          {{ row.name || row.steam_id }}
                        </div>
                        <div class="text-xs text-muted-foreground">
                          {{ row.rank_name }}
                        </div>
                      </div>
                      <span
                        class="shrink-0 font-mono text-sm text-[hsl(var(--tac-amber))]"
                        >{{ row.points }}</span
                      >
                    </li>
                  </ul>
                  <p
                    v-else
                    class="rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
                  >
                    {{ $t("pages.public_servers.details.ranks_empty") }}
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
