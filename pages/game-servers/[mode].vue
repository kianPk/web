<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import {
  Check,
  CircleHelp,
  Copy,
  KeyRound,
  LayoutGrid,
  List,
  Loader2,
  MapPin,
  Play,
  Settings,
  Users,
} from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import { toast } from "@/components/ui/toast";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import ServerLiveDialog from "~/components/servers/ServerLiveDialog.vue";
import { mapLabel } from "~/utilities/serverModes";
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
const modeName = computed(() =>
  mode.value ? t(`pages.servers.modes.${mode.value.key}.name`) : "",
);

type SortKey = "players_desc" | "players_asc" | "number";
type ViewKey = "tiles" | "table";

const ANY = "all";
const mapFilter = ref(ANY);
const regionFilter = ref(ANY);
const sortKey = ref<SortKey>("players_desc");
const showFull = ref(true);
const view = ref<ViewKey>("tiles");
const quickPlayMap = ref(ANY);

const quickPlayMapKey = computed(() => `servers:qp-map:${modeKey.value}`);
function loadQuickPlayMap() {
  quickPlayMap.value = localStorage.getItem(quickPlayMapKey.value) || ANY;
}
onMounted(() => {
  const saved = localStorage.getItem("servers:view");
  if (saved === "tiles" || saved === "table") view.value = saved;
  loadQuickPlayMap();
});
watch(view, (value) => localStorage.setItem("servers:view", value));
watch(quickPlayMap, (value) =>
  localStorage.setItem(quickPlayMapKey.value, value),
);
watch(modeKey, () => {
  mapFilter.value = ANY;
  regionFilter.value = ANY;
  loadQuickPlayMap();
});

// The pool operators keep in the settings; the mode's built-in list until it
// arrives or if the api cannot be reached.
const livePool = ref<string[] | null>(null);
const mapPool = computed(() => livePool.value ?? mode.value?.maps ?? []);
async function loadPool() {
  livePool.value = null;
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const { maps } = await $fetch<{ maps: Array<{ name: string }> }>(
      `https://${apiDomain}/hosted-servers/section-maps/${modeKey.value}`,
    );
    livePool.value = maps.map((map) => map.name);
  } catch {
    livePool.value = null;
  }
}
onMounted(loadPool);
watch(modeKey, loadPool);

function distinct(values: Array<string | null>) {
  return [...new Set(values.filter((v): v is string => !!v))].sort();
}
const maps = computed(() =>
  distinct((mode.value?.servers || []).map((s) => s.map)).filter(
    (m) => m !== "default",
  ),
);
const regions = computed(() =>
  distinct((mode.value?.servers || []).map((s) => s.regionName)),
);

const visibleServers = computed(() => {
  let list = [...(mode.value?.servers || [])];
  if (mapFilter.value !== ANY) {
    list = list.filter((s) => s.map === mapFilter.value);
  }
  if (regionFilter.value !== ANY) {
    list = list.filter((s) => s.regionName === regionFilter.value);
  }
  if (!showFull.value) {
    list = list.filter((s) => !s.full);
  }
  const byNumber = (a: FleetServer, b: FleetServer) => a.number - b.number;
  switch (sortKey.value) {
    case "players_asc":
      return list.sort((a, b) => a.players - b.players || byNumber(a, b));
    case "number":
      return list.sort(byNumber);
    default:
      return list.sort((a, b) => b.players - a.players || byNumber(a, b));
  }
});

const quickPlayTarget = computed(() =>
  pickQuickPlayServer(
    mode.value?.servers || [],
    quickPlayMap.value === ANY ? null : quickPlayMap.value,
  ),
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

async function copyText(text: string | null, title: string) {
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
    toast({ title });
  } catch {
    toast({ title: t("pages.servers.copy_failed"), variant: "destructive" });
  }
}

const commandsOpen = ref(false);
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

function playersTone(server: FleetServer) {
  if (server.full) return "text-red-400";
  if (server.players > 0) return "text-emerald-400";
  return "text-muted-foreground";
}

function mapImage(map: string) {
  return `/img/maps/screenshots/${map}.webp`;
}
function onImgError(e: Event) {
  const img = e.target as HTMLImageElement;
  const fallback = mode.value?.cover || "/img/maps/screenshots/default.webp";
  if (!img.src.endsWith(fallback)) img.src = fallback;
}

useHead(() => ({
  title: modeName.value || t("pages.servers.title"),
}));
</script>

<template>
  <PageTransition :delay="0">
    <nav class="-mx-1 flex gap-1 overflow-x-auto px-1 pb-1">
      <NuxtLink
        v-for="m in modes"
        :key="m.key"
        :to="`/game-servers/${m.key}`"
        class="flex min-w-24 shrink-0 flex-col items-center rounded-lg px-4 py-2 transition-colors"
        :class="
          m.key === modeKey
            ? 'bg-card text-foreground ring-1 ring-white/10'
            : 'text-muted-foreground hover:bg-card/60 hover:text-foreground'
        "
      >
        <span class="text-sm font-bold">{{
          $t(`pages.servers.modes.${m.key}.name`)
        }}</span>
        <span
          class="inline-flex items-center gap-1 text-[0.7rem] tabular-nums"
          :class="m.players > 0 ? 'text-emerald-400' : 'opacity-60'"
        >
          <Users class="h-3 w-3" />{{ m.players }}
        </span>
      </NuxtLink>
    </nav>
  </PageTransition>

  <div v-if="!ready" class="mt-4 space-y-4">
    <Skeleton class="h-52 w-full rounded-2xl" />
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <Skeleton v-for="i in 6" :key="i" class="h-[88px] rounded-xl" />
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
        class="relative mt-3 overflow-hidden rounded-2xl bg-card ring-1 ring-white/5"
      >
        <img
          :src="mode.cover"
          alt=""
          class="absolute inset-0 h-full w-full object-cover opacity-40"
          @error="onImgError"
        />
        <div
          class="absolute inset-0 bg-gradient-to-r from-card via-card/80 to-card/10 rtl:bg-gradient-to-l"
        />

        <div class="relative flex flex-col gap-5 p-5 md:p-7">
          <div class="space-y-2">
            <h1 class="text-3xl font-extrabold tracking-tight md:text-4xl">
              {{ modeName }}
            </h1>
            <div
              class="flex flex-wrap items-center gap-x-5 gap-y-1 text-xs text-white/70"
            >
              <TooltipProvider :delay-duration="100">
                <Tooltip>
                  <TooltipTrigger as-child>
                    <button
                      type="button"
                      class="inline-flex items-center gap-1 hover:text-white"
                    >
                      {{ $t("pages.servers.about_mode") }}
                      <CircleHelp class="h-3.5 w-3.5" />
                    </button>
                  </TooltipTrigger>
                  <TooltipContent class="max-w-xs text-xs leading-relaxed">
                    {{ $t(`pages.servers.modes.${mode.key}.about`) }}
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
              <button
                type="button"
                class="inline-flex items-center gap-1.5 hover:text-white"
                @click="commandsOpen = true"
              >
                {{ $t("pages.servers.server_commands") }}
                <span
                  class="inline-flex h-5 w-5 items-center justify-center rounded bg-white/10"
                >
                  <KeyRound class="h-3 w-3" />
                </span>
              </button>
            </div>
            <p class="max-w-xl pt-1 text-sm leading-relaxed text-white/80">
              {{ $t(`pages.servers.modes.${mode.key}.subtitle`) }}
            </p>
          </div>

          <div class="flex flex-wrap items-center gap-2">
            <Popover>
              <PopoverTrigger as-child>
                <Button
                  variant="secondary"
                  size="icon"
                  class="h-12 w-12 shrink-0 rounded-xl"
                  :title="$t('pages.servers.quick_play_settings')"
                >
                  <Settings class="h-5 w-5" />
                </Button>
              </PopoverTrigger>
              <PopoverContent align="start" class="w-64 space-y-2">
                <p class="text-sm font-semibold">
                  {{ $t("pages.servers.quick_play_settings") }}
                </p>
                <p class="text-xs text-muted-foreground">
                  {{ $t("pages.servers.quick_play_map") }}
                </p>
                <Select v-model="quickPlayMap">
                  <SelectTrigger class="h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem :value="ANY">{{
                      $t("pages.servers.any_map")
                    }}</SelectItem>
                    <SelectItem v-for="map in maps" :key="map" :value="map">
                      {{ mapLabel(map) }}
                    </SelectItem>
                  </SelectContent>
                </Select>
              </PopoverContent>
            </Popover>

            <Button
              class="tac-amber-cta h-12 min-w-52 flex-col gap-0 rounded-xl px-6 leading-tight"
              :disabled="!quickPlayTarget || joining"
              @click="quickPlay"
            >
              <span class="inline-flex items-center gap-2 text-base font-bold">
                {{ $t("pages.servers.quick_play") }}
                <Loader2 v-if="joining" class="h-4 w-4 animate-spin" />
                <Play v-else class="h-4 w-4 fill-current" />
              </span>
              <span class="text-[0.65rem] font-medium opacity-75">
                {{
                  quickPlayMap === ANY
                    ? $t("pages.servers.any_map")
                    : mapLabel(quickPlayMap)
                }}
              </span>
            </Button>

            <p v-if="!quickPlayTarget" class="text-xs text-muted-foreground">
              {{ $t("pages.servers.quick_play_none") }}
            </p>
          </div>
        </div>
      </section>
    </PageTransition>

    <div class="mt-4 flex flex-col gap-4 lg:flex-row lg:items-start">
      <div class="min-w-0 flex-1">
        <PageTransition :delay="80">
          <div class="flex flex-wrap items-center gap-2">
            <div
              class="inline-flex h-9 items-center rounded-lg bg-card p-0.5 ring-1 ring-white/5"
            >
              <button
                type="button"
                class="inline-flex h-full items-center gap-1.5 rounded-md px-2.5 text-xs"
                :class="
                  view === 'tiles'
                    ? 'bg-muted text-foreground'
                    : 'text-muted-foreground'
                "
                @click="view = 'tiles'"
              >
                <LayoutGrid class="h-4 w-4" />
                <span class="hidden sm:inline">{{
                  $t("pages.servers.filters.view_tiles")
                }}</span>
              </button>
              <button
                type="button"
                class="inline-flex h-full items-center gap-1.5 rounded-md px-2.5 text-xs"
                :class="
                  view === 'table'
                    ? 'bg-muted text-foreground'
                    : 'text-muted-foreground'
                "
                @click="view = 'table'"
              >
                <List class="h-4 w-4" />
                <span class="hidden sm:inline">{{
                  $t("pages.servers.filters.view_table")
                }}</span>
              </button>
            </div>

            <Select v-model="mapFilter">
              <SelectTrigger class="h-9 w-40 bg-card">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem :value="ANY">{{
                  $t("pages.servers.filters.all_maps")
                }}</SelectItem>
                <SelectItem v-for="map in maps" :key="map" :value="map">
                  {{ mapLabel(map) }}
                </SelectItem>
              </SelectContent>
            </Select>

            <Select v-model="regionFilter">
              <SelectTrigger class="h-9 w-40 bg-card">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem :value="ANY">{{
                  $t("pages.servers.filters.all_locations")
                }}</SelectItem>
                <SelectItem
                  v-for="region in regions"
                  :key="region"
                  :value="region"
                >
                  {{ region }}
                </SelectItem>
              </SelectContent>
            </Select>

            <button
              type="button"
              class="inline-flex h-9 items-center gap-2 rounded-lg bg-card px-3 text-xs ring-1 ring-white/5 transition-colors"
              :class="showFull ? 'text-foreground' : 'text-muted-foreground'"
              @click="showFull = !showFull"
            >
              <span
                class="inline-flex h-4 w-4 items-center justify-center rounded border"
                :class="
                  showFull
                    ? 'border-[hsl(var(--tac-amber))] bg-[hsl(var(--tac-amber))] text-black'
                    : 'border-white/30'
                "
              >
                <Check v-if="showFull" class="h-3 w-3" />
              </span>
              {{ $t("pages.servers.filters.show_full") }}
            </button>

            <Select v-model="sortKey">
              <SelectTrigger class="ms-auto h-9 w-48 bg-card">
                <span class="text-muted-foreground"
                  >{{ $t("pages.servers.filters.sort_by") }}:</span
                >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="players_desc">{{
                  $t("pages.servers.filters.sort_players_desc")
                }}</SelectItem>
                <SelectItem value="players_asc">{{
                  $t("pages.servers.filters.sort_players_asc")
                }}</SelectItem>
                <SelectItem value="number">{{
                  $t("pages.servers.filters.sort_number")
                }}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </PageTransition>

        <PageTransition :delay="120">
          <Empty
            v-if="mode.servers.length === 0"
            class="mt-4 min-h-[200px] rounded-xl bg-card"
          >
            <EmptyTitle>{{ $t("pages.servers.no_servers_title") }}</EmptyTitle>
            <EmptyDescription>{{
              $t("pages.servers.no_servers")
            }}</EmptyDescription>
          </Empty>

          <p
            v-else-if="visibleServers.length === 0"
            class="mt-8 text-center text-sm text-muted-foreground"
          >
            {{ $t("pages.servers.no_match_filters") }}
          </p>

          <div
            v-else-if="view === 'tiles'"
            class="mt-3 grid grid-cols-1 gap-2.5 sm:grid-cols-2"
          >
            <div
              v-for="server in visibleServers"
              :key="server.id"
              role="button"
              tabindex="0"
              class="group relative flex h-[88px] cursor-pointer items-stretch overflow-hidden rounded-xl bg-card ring-1 ring-white/5 transition-all hover:ring-white/20"
              @click="openServer(server)"
              @keydown.enter="openServer(server)"
            >
              <img
                :src="mapImage(server.map)"
                alt=""
                class="absolute inset-0 h-full w-full object-cover opacity-30 transition-all duration-300 group-hover:scale-105 group-hover:opacity-45"
                @error="onImgError"
              />
              <div
                class="absolute inset-0 bg-gradient-to-r from-card via-card/70 to-transparent rtl:bg-gradient-to-l"
              />

              <div
                class="relative flex min-w-0 flex-1 flex-col justify-between px-4 py-3"
              >
                <div class="flex items-center gap-2 text-sm">
                  <span class="font-extrabold">#{{ server.number }}</span>
                  <span
                    v-if="server.regionName"
                    class="inline-flex min-w-0 items-center gap-1 text-xs text-white/60"
                  >
                    <MapPin class="h-3 w-3 shrink-0" />
                    <span class="truncate">{{ server.regionName }}</span>
                  </span>
                </div>
                <div class="flex items-center gap-3 text-xs">
                  <span
                    class="inline-flex items-center gap-1 font-semibold tabular-nums"
                    :class="playersTone(server)"
                  >
                    <Users class="h-3.5 w-3.5" />
                    {{ server.players }}/{{ server.capacity }}
                  </span>
                  <span class="truncate text-white/80">{{
                    mapLabel(server.map)
                  }}</span>
                </div>
              </div>

              <div class="relative flex items-center gap-1.5 pe-3">
                <button
                  type="button"
                  class="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-white/10 text-white/80 transition-colors hover:bg-white/20 hover:text-white"
                  :title="$t('pages.servers.copy_ip')"
                  @click.stop="
                    copyText(
                      server.connection_string,
                      $t('pages.servers.copied_ip'),
                    )
                  "
                >
                  <Copy class="h-4 w-4" />
                </button>
                <button
                  type="button"
                  class="inline-flex h-9 w-9 items-center justify-center rounded-lg transition-colors"
                  :class="
                    server.full
                      ? 'cursor-not-allowed bg-white/5 text-white/30'
                      : 'bg-[hsl(var(--tac-amber))] text-black hover:brightness-110'
                  "
                  :disabled="server.full"
                  :title="
                    server.full
                      ? $t('pages.servers.full')
                      : $t('pages.servers.join')
                  "
                  @click.stop="connect(server)"
                >
                  <Play class="h-4 w-4 fill-current" />
                </button>
              </div>
            </div>
          </div>

          <div
            v-else
            class="mt-3 overflow-x-auto rounded-xl bg-card ring-1 ring-white/5"
          >
            <table class="w-full text-sm">
              <thead>
                <tr
                  class="border-b border-white/5 text-xs text-muted-foreground"
                >
                  <th class="w-14 px-4 py-2.5 text-start font-medium">#</th>
                  <th class="px-3 py-2.5 text-start font-medium">
                    {{ $t("pages.servers.table.players") }}
                  </th>
                  <th class="px-3 py-2.5 text-start font-medium">
                    {{ $t("pages.servers.table.map") }}
                  </th>
                  <th
                    class="hidden px-3 py-2.5 text-start font-medium sm:table-cell"
                  >
                    {{ $t("pages.servers.table.location") }}
                  </th>
                  <th class="px-4 py-2.5" />
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="server in visibleServers"
                  :key="server.id"
                  class="cursor-pointer border-b border-white/5 last:border-b-0 hover:bg-white/[0.03]"
                  @click="openServer(server)"
                >
                  <td class="px-4 py-2.5 font-bold">{{ server.number }}</td>
                  <td
                    class="px-3 py-2.5 font-semibold tabular-nums"
                    :class="playersTone(server)"
                  >
                    {{ server.players }}/{{ server.capacity }}
                  </td>
                  <td class="px-3 py-2.5">{{ mapLabel(server.map) }}</td>
                  <td
                    class="hidden px-3 py-2.5 text-muted-foreground sm:table-cell"
                  >
                    {{ server.regionName }}
                  </td>
                  <td class="px-4 py-1.5 text-end">
                    <Button
                      size="sm"
                      class="h-8 min-w-20 rounded-lg"
                      :class="server.full ? '' : 'tac-amber-cta'"
                      :variant="server.full ? 'secondary' : 'default'"
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
      </div>

      <PageTransition :delay="160">
        <aside class="w-full shrink-0 space-y-3 lg:w-72">
          <div class="rounded-xl bg-card p-4 ring-1 ring-white/5">
            <p class="text-sm font-bold">{{ $t("pages.servers.map_pool") }}</p>
            <div class="mt-3 flex flex-wrap gap-1.5">
              <span
                v-for="map in mapPool"
                :key="map"
                class="rounded-md bg-white/5 px-2 py-1 text-xs"
                dir="ltr"
                >{{ map }}</span
              >
            </div>
          </div>

          <div class="rounded-xl bg-card p-4 ring-1 ring-white/5">
            <div class="flex items-center justify-between">
              <p class="text-sm font-bold">
                {{ $t("pages.servers.commands_title") }}
              </p>
              <button
                type="button"
                class="text-xs text-muted-foreground hover:text-foreground"
                @click="commandsOpen = true"
              >
                {{ $t("pages.servers.show_all") }}
              </button>
            </div>
            <ul class="mt-3 space-y-2">
              <li
                v-for="cmd in mode.commands.slice(0, 4)"
                :key="cmd.command"
                class="flex items-baseline gap-2 text-xs"
              >
                <code
                  class="shrink-0 font-mono font-semibold text-[hsl(var(--tac-amber))]"
                  dir="ltr"
                  >{{ cmd.command }}</code
                >
                <span class="text-muted-foreground">{{
                  $t(`pages.servers.commands.${cmd.descriptionKey}`)
                }}</span>
              </li>
            </ul>
          </div>
        </aside>
      </PageTransition>
    </div>

    <PageTransition :delay="200">
      <section class="mt-8">
        <h2 class="text-xl font-extrabold tracking-tight">
          {{ $t("pages.servers.faq_title") }}
        </h2>
        <Accordion type="single" collapsible class="mt-3 space-y-2">
          <AccordionItem
            v-for="i in mode.faq"
            :key="i"
            :value="`q${i}`"
            class="rounded-xl border-0 bg-card px-4 ring-1 ring-white/5"
          >
            <AccordionTrigger class="text-start text-sm font-semibold">
              {{ $t(`pages.servers.modes.${mode.key}.faq.q${i}`) }}
            </AccordionTrigger>
            <AccordionContent
              class="text-sm leading-relaxed text-muted-foreground"
            >
              {{ $t(`pages.servers.modes.${mode.key}.faq.a${i}`) }}
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      </section>
    </PageTransition>

    <Dialog v-model:open="commandsOpen">
      <DialogContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ $t("pages.servers.commands_title") }}</DialogTitle>
          <DialogDescription>{{
            $t("pages.servers.commands_hint")
          }}</DialogDescription>
        </DialogHeader>
        <ul class="space-y-1">
          <li v-for="cmd in mode.commands" :key="cmd.command">
            <button
              type="button"
              class="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-start transition-colors hover:bg-muted/60"
              @click="copyText(cmd.command, $t('pages.servers.copied_command'))"
            >
              <code
                class="w-24 shrink-0 font-mono text-sm font-semibold text-[hsl(var(--tac-amber))]"
                dir="ltr"
                >{{ cmd.command }}</code
              >
              <span class="flex-1 text-sm text-muted-foreground">{{
                $t(`pages.servers.commands.${cmd.descriptionKey}`)
              }}</span>
              <Copy class="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            </button>
          </li>
        </ul>
      </DialogContent>
    </Dialog>
  </template>

  <ServerLiveDialog
    v-model:open="dialogOpen"
    :server="selected"
    :mode-name="modeName"
  />
</template>
