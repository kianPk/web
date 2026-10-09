<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import {
  ChevronLeft,
  LayoutGrid,
  List,
  Loader2,
  MapPin,
  Play,
  Terminal,
  Users,
} from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "@/components/ui/toast";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import ServerLiveDialog from "~/components/servers/ServerLiveDialog.vue";
import cleanMapName from "~/utilities/cleanMapName";
import {
  acAllowsJoin,
  pickQuickPlayServer,
  usePublicServerFleet,
  type FleetMode,
  type FleetServer,
} from "~/composables/usePublicServerFleet";

const route = useRoute();
const { t } = useI18n();
const { loading, servers, modes } = usePublicServerFleet();

const modeKey = computed(() => String(route.params.mode || ""));
const mode = computed<FleetMode | null>(
  () => modes.value.find((m) => m.key === modeKey.value) ?? null,
);
const ready = computed(() => !loading.value || servers.value.length > 0);

function modeName(m: FleetMode | null) {
  if (!m) return "";
  return m.i18nKey ? t(`pages.servers.modes.${m.i18nKey}.name`) : m.name || "";
}
function modeDescription(m: FleetMode | null) {
  if (!m) return "";
  return m.i18nKey
    ? t(`pages.servers.modes.${m.i18nKey}.description`)
    : m.description || "";
}

type SortKey = "players_desc" | "players_asc" | "name";
type ViewKey = "tiles" | "table";

const mapFilter = ref("all");
const sortKey = ref<SortKey>("players_desc");
const showFull = ref(true);
const view = ref<ViewKey>("tiles");
onMounted(() => {
  const saved = localStorage.getItem("servers:view");
  if (saved === "tiles" || saved === "table") view.value = saved;
});
watch(view, (value) => localStorage.setItem("servers:view", value));
watch(modeKey, () => {
  mapFilter.value = "all";
});

const maps = computed(() => {
  const set = new Set<string>();
  for (const s of mode.value?.servers || []) {
    if (s.map !== "default") set.add(s.map);
  }
  return [...set].sort();
});

const visibleServers = computed(() => {
  let list = [...(mode.value?.servers || [])];
  if (mapFilter.value !== "all") {
    list = list.filter((s) => s.map === mapFilter.value);
  }
  if (!showFull.value) {
    list = list.filter((s) => !s.full);
  }
  const byName = (a: FleetServer, b: FleetServer) =>
    (a.label || "").localeCompare(b.label || "");
  switch (sortKey.value) {
    case "players_asc":
      return list.sort((a, b) => a.players - b.players || byName(a, b));
    case "name":
      return list.sort(byName);
    default:
      return list.sort((a, b) => b.players - a.players || byName(a, b));
  }
});

const quickPlayTarget = computed(() =>
  pickQuickPlayServer(mode.value?.servers || []),
);
const joining = ref(false);

async function connect(server: FleetServer | null) {
  if (!server?.connection_link) return;
  joining.value = true;
  try {
    if (!(await acAllowsJoin())) {
      toast({
        title: t("ac.title"),
        description: t("ac.join_need_ac"),
        variant: "destructive",
      });
      return;
    }
    window.location.href = server.connection_link;
  } finally {
    window.setTimeout(() => (joining.value = false), 4000);
  }
}

async function quickPlay() {
  const target = quickPlayTarget.value;
  if (!target) {
    toast({
      title: t("pages.servers.quick_play_none"),
      variant: "destructive",
    });
    return;
  }
  await connect(target);
}

const dialogOpen = ref(false);
const selected = ref<FleetServer | null>(null);
function openServer(server: FleetServer) {
  selected.value = server;
  dialogOpen.value = true;
}
watch(servers, (list) => {
  if (!selected.value) return;
  selected.value = list.find((s) => s.id === selected.value!.id) ?? null;
  if (!selected.value) dialogOpen.value = false;
});

function capacityTone(server: FleetServer) {
  const pct = server.capacity ? server.players / server.capacity : 0;
  if (pct >= 0.9) return "text-red-400";
  if (pct >= 0.6) return "text-amber-400";
  if (server.players > 0) return "text-emerald-400";
  return "text-muted-foreground";
}

function onImgError(e: Event) {
  (e.target as HTMLImageElement).src = "/img/maps/screenshots/default.webp";
}

useHead(() => ({
  title: mode.value ? modeName(mode.value) : t("pages.servers.title"),
}));
</script>

<template>
  <PageTransition :delay="0">
    <div class="flex items-center gap-2">
      <Button as-child variant="ghost" size="sm" class="-ms-2">
        <NuxtLink to="/game-servers">
          <ChevronLeft class="h-4 w-4 rtl:rotate-180" />
          {{ $t("pages.servers.title") }}
        </NuxtLink>
      </Button>
    </div>

    <div
      v-if="modes.length > 1"
      class="mt-2 flex gap-1 overflow-x-auto border-b border-border/60 pb-px"
    >
      <NuxtLink
        v-for="m in modes"
        :key="m.key"
        :to="`/game-servers/${m.key}`"
        class="flex shrink-0 flex-col items-center rounded-t-md px-3 py-2 text-xs transition-colors"
        :class="
          m.key === modeKey
            ? 'bg-[hsl(var(--tac-amber)/0.12)] text-foreground shadow-[inset_0_-2px_0_hsl(var(--tac-amber))]'
            : 'text-muted-foreground hover:bg-muted/40 hover:text-foreground'
        "
      >
        <span class="font-semibold">{{ modeName(m) }}</span>
        <span
          class="font-mono text-[0.65rem] tabular-nums"
          :class="m.players > 0 ? 'text-emerald-400' : 'opacity-60'"
          >{{ m.players }}</span
        >
      </NuxtLink>
    </div>
  </PageTransition>

  <div v-if="!ready" class="mt-5 space-y-4">
    <Skeleton class="h-44 w-full rounded-xl" />
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <Skeleton v-for="i in 6" :key="i" class="h-20 rounded-lg" />
    </div>
  </div>

  <Empty v-else-if="!mode" class="mt-6 min-h-[220px]">
    <EmptyTitle>{{ $t("pages.servers.mode_missing_title") }}</EmptyTitle>
    <EmptyDescription>{{ $t("pages.servers.mode_missing") }}</EmptyDescription>
    <Button as-child>
      <NuxtLink to="/game-servers">{{
        $t("pages.servers.back_to_modes")
      }}</NuxtLink>
    </Button>
  </Empty>

  <template v-else>
    <PageTransition :delay="40">
      <section
        class="relative mt-5 overflow-hidden rounded-xl border border-border/60"
      >
        <img
          :src="mode.cover"
          alt=""
          class="absolute inset-0 h-full w-full object-cover opacity-35"
          @error="onImgError"
        />
        <div
          class="absolute inset-0 bg-gradient-to-r from-background via-background/85 to-background/40 rtl:bg-gradient-to-l"
        />
        <div
          class="absolute inset-y-0 start-0 w-1"
          :style="{ background: mode.accent }"
        />

        <div
          class="relative flex flex-col gap-5 p-5 md:flex-row md:items-end md:justify-between md:p-7"
        >
          <div class="max-w-xl space-y-3">
            <h1 class="text-3xl font-extrabold tracking-tight md:text-4xl">
              {{ modeName(mode) }}
            </h1>
            <p class="text-sm leading-relaxed text-muted-foreground">
              {{ modeDescription(mode) }}
            </p>
            <div
              class="flex flex-wrap items-center gap-x-4 gap-y-1 font-mono text-xs text-muted-foreground"
            >
              <span class="inline-flex items-center gap-1.5">
                <Users class="h-3.5 w-3.5 text-emerald-400" />
                {{ $t("pages.servers.in_game", { count: mode.players }) }}
              </span>
              <span>
                {{
                  $t("pages.servers.server_count", {
                    count: mode.servers.length,
                  })
                }}
              </span>
            </div>

            <div v-if="mode.commands.length" class="space-y-1.5 pt-1">
              <p
                class="inline-flex items-center gap-1.5 font-mono text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-muted-foreground"
              >
                <Terminal class="h-3 w-3" />
                {{ $t("pages.servers.commands_title") }}
              </p>
              <div class="flex flex-wrap gap-1.5">
                <span
                  v-for="cmd in mode.commands"
                  :key="cmd.command"
                  class="inline-flex items-center gap-1.5 rounded-md border border-border/60 bg-background/60 px-2 py-1 text-xs backdrop-blur-sm"
                >
                  <code
                    class="font-mono font-semibold text-[hsl(var(--tac-amber))]"
                    dir="ltr"
                    >{{ cmd.command }}</code
                  >
                  <span class="text-muted-foreground">{{
                    $t(`pages.servers.commands.${cmd.descriptionKey}`)
                  }}</span>
                </span>
              </div>
            </div>
          </div>

          <div class="flex shrink-0 flex-col items-stretch gap-1.5 md:items-end">
            <Button
              size="lg"
              class="tac-amber-cta h-12 min-w-48 text-base font-bold"
              :disabled="!quickPlayTarget || joining"
              @click="quickPlay"
            >
              <Loader2 v-if="joining" class="h-5 w-5 animate-spin" />
              <Play v-else class="h-5 w-5" />
              {{ $t("pages.servers.quick_play") }}
            </Button>
            <p
              class="text-center text-[0.7rem] text-muted-foreground md:text-end"
            >
              {{
                quickPlayTarget
                  ? $t("pages.servers.quick_play_hint", {
                      server: quickPlayTarget.label,
                    })
                  : $t("pages.servers.quick_play_none")
              }}
            </p>
          </div>
        </div>
      </section>
    </PageTransition>

    <PageTransition :delay="80">
      <div class="mt-5 flex flex-wrap items-center gap-2">
        <Select v-model="mapFilter">
          <SelectTrigger class="h-9 w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{{
              $t("pages.servers.filters.all_maps")
            }}</SelectItem>
            <SelectItem v-for="map in maps" :key="map" :value="map">
              {{ cleanMapName(map) }}
            </SelectItem>
          </SelectContent>
        </Select>

        <Select v-model="sortKey">
          <SelectTrigger class="h-9 w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="players_desc">{{
              $t("pages.servers.filters.sort_players_desc")
            }}</SelectItem>
            <SelectItem value="players_asc">{{
              $t("pages.servers.filters.sort_players_asc")
            }}</SelectItem>
            <SelectItem value="name">{{
              $t("pages.servers.filters.sort_name")
            }}</SelectItem>
          </SelectContent>
        </Select>

        <label
          class="inline-flex h-9 cursor-pointer items-center gap-2 rounded-md border border-border/60 px-3 text-xs"
        >
          <Switch
            v-model="showFull"
            class="data-[state=checked]:bg-[hsl(var(--tac-amber))] data-[state=unchecked]:bg-muted/70"
          />
          {{ $t("pages.servers.filters.show_full") }}
        </label>

        <div
          class="ms-auto inline-flex h-9 items-center rounded-md border border-border/60 p-0.5"
        >
          <button
            type="button"
            class="inline-flex h-full items-center rounded px-2"
            :class="
              view === 'tiles'
                ? 'bg-muted text-foreground'
                : 'text-muted-foreground'
            "
            :title="$t('pages.servers.filters.view_tiles')"
            @click="view = 'tiles'"
          >
            <LayoutGrid class="h-4 w-4" />
          </button>
          <button
            type="button"
            class="inline-flex h-full items-center rounded px-2"
            :class="
              view === 'table'
                ? 'bg-muted text-foreground'
                : 'text-muted-foreground'
            "
            :title="$t('pages.servers.filters.view_table')"
            @click="view = 'table'"
          >
            <List class="h-4 w-4" />
          </button>
        </div>
      </div>
    </PageTransition>

    <PageTransition :delay="120">
      <p
        v-if="visibleServers.length === 0"
        class="mt-8 text-center text-sm text-muted-foreground"
      >
        {{ $t("pages.servers.no_match_filters") }}
      </p>

      <div
        v-else-if="view === 'tiles'"
        class="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3"
      >
        <button
          v-for="server in visibleServers"
          :key="server.id"
          type="button"
          class="group relative flex h-20 items-stretch overflow-hidden rounded-lg border border-border/60 bg-card text-start transition-all hover:border-[hsl(var(--tac-amber)/0.5)]"
          @click="openServer(server)"
        >
          <img
            :src="`/img/maps/screenshots/${server.map}.webp`"
            alt=""
            class="absolute inset-0 h-full w-full object-cover opacity-25 transition-opacity group-hover:opacity-35"
            @error="onImgError"
          />
          <div
            class="absolute inset-0 bg-gradient-to-r from-card via-card/80 to-transparent rtl:bg-gradient-to-l"
          />
          <div
            class="relative flex min-w-0 flex-1 flex-col justify-center gap-1 px-3"
          >
            <p class="truncate text-sm font-semibold">{{ server.label }}</p>
            <div
              class="flex items-center gap-3 font-mono text-[0.7rem] text-muted-foreground"
            >
              <span
                class="font-semibold tabular-nums"
                :class="capacityTone(server)"
                >{{ server.players }}/{{ server.capacity }}</span
              >
              <span class="truncate">{{ cleanMapName(server.map) }}</span>
              <span
                v-if="server.region"
                class="hidden items-center gap-1 sm:inline-flex"
              >
                <MapPin class="h-3 w-3" />{{ server.region }}
              </span>
            </div>
          </div>
          <div class="relative flex items-center pe-3">
            <span
              class="inline-flex h-8 items-center gap-1 rounded-md border border-border/60 bg-background/60 px-2.5 text-xs font-semibold transition-colors group-hover:border-[hsl(var(--tac-amber)/0.6)] group-hover:text-[hsl(var(--tac-amber))]"
              :class="{ 'opacity-50': server.full }"
              @click.stop="server.full ? openServer(server) : connect(server)"
            >
              <Play class="h-3 w-3" />
              {{
                server.full
                  ? $t("pages.servers.full")
                  : $t("pages.servers.join")
              }}
            </span>
          </div>
        </button>
      </div>

      <div
        v-else
        class="mt-4 overflow-x-auto rounded-lg border border-border/60"
      >
        <table class="w-full text-sm">
          <thead>
            <tr
              class="border-b border-border/60 font-mono text-[0.6rem] uppercase tracking-[0.14em] text-muted-foreground"
            >
              <th class="px-3 py-2 text-start font-semibold">
                {{ $t("pages.servers.table.server") }}
              </th>
              <th class="px-3 py-2 text-start font-semibold">
                {{ $t("pages.servers.table.players") }}
              </th>
              <th class="px-3 py-2 text-start font-semibold">
                {{ $t("pages.servers.table.map") }}
              </th>
              <th
                class="hidden px-3 py-2 text-start font-semibold sm:table-cell"
              >
                {{ $t("pages.servers.table.region") }}
              </th>
              <th class="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="server in visibleServers"
              :key="server.id"
              class="cursor-pointer border-b border-border/40 last:border-b-0 hover:bg-muted/30"
              @click="openServer(server)"
            >
              <td class="max-w-[14rem] truncate px-3 py-2 font-medium">
                {{ server.label }}
              </td>
              <td
                class="px-3 py-2 font-mono tabular-nums"
                :class="capacityTone(server)"
              >
                {{ server.players }}/{{ server.capacity }}
              </td>
              <td class="px-3 py-2 text-muted-foreground">
                {{ cleanMapName(server.map) }}
              </td>
              <td class="hidden px-3 py-2 text-muted-foreground sm:table-cell">
                {{ server.region }}
              </td>
              <td class="px-3 py-1.5 text-end">
                <Button
                  size="sm"
                  variant="outline"
                  class="h-7"
                  :disabled="server.full"
                  @click.stop="connect(server)"
                >
                  {{
                    server.full
                      ? $t("pages.servers.full")
                      : $t("pages.servers.join")
                  }}
                </Button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </PageTransition>
  </template>

  <ServerLiveDialog
    v-model:open="dialogOpen"
    :server="selected"
    :mode-name="modeName(mode)"
  />
</template>
