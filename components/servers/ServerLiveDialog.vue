<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from "vue";
import { ArrowUpRight, MapPin, Users } from "lucide-vue-next";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import QuickServerConnect from "~/components/match/QuickServerConnect.vue";
import { mapLabel } from "~/utilities/serverModes";
import { csRankIcon } from "~/utilities/csRank";
import type { FleetServer } from "~/composables/usePublicServerFleet";

type LivePlayer = {
  steam_id: string;
  name: string | null;
  avatar_url: string | null;
  registered: boolean;
  points: number;
  skill_group?: number;
  rank_name?: string;
};

const props = defineProps<{
  server: FleetServer | null;
  modeName: string;
}>();

const open = defineModel<boolean>("open", { default: false });

const players = ref<LivePlayer[]>([]);
const loading = ref(false);
let timer: number | undefined;

async function load() {
  const id = props.server?.id;
  if (!id) return;
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain as string;
    const data = await $fetch<{ players: LivePlayer[] }>(
      `https://${apiDomain}/hosted-servers/public-details/${id}/players`,
      { credentials: "include" },
    );
    if (props.server?.id === id) {
      players.value = data?.players || [];
    }
  } catch {
    players.value = [];
  } finally {
    loading.value = false;
  }
}

watch(
  () => [open.value, props.server?.id] as const,
  ([isOpen, id]) => {
    window.clearInterval(timer);
    if (!isOpen || !id) return;
    players.value = [];
    loading.value = true;
    void load();
    timer = window.setInterval(() => void load(), 20_000);
  },
);

onUnmounted(() => window.clearInterval(timer));

const capacityPercent = computed(() => {
  const s = props.server;
  if (!s?.capacity) return 0;
  return Math.min(100, Math.round((s.players / s.capacity) * 100));
});

function onImgError(e: Event) {
  (e.target as HTMLImageElement).src = "/img/maps/screenshots/default.webp";
}

function onAvatarError(e: Event) {
  (e.target as HTMLImageElement).style.visibility = "hidden";
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent
      class="max-w-2xl gap-0 overflow-hidden border-border/70 p-0"
    >
      <template v-if="server">
        <div class="relative h-36 overflow-hidden">
          <img
            :src="`/img/maps/screenshots/${server.map}.webp`"
            :alt="server.map"
            class="h-full w-full object-cover"
            @error="onImgError"
          />
          <div
            class="absolute inset-0 bg-gradient-to-t from-background via-background/70 to-black/20"
          />
          <div class="absolute inset-x-0 bottom-0 space-y-1.5 p-5">
            <DialogTitle class="text-xl font-bold tracking-tight">
              <span class="font-mono text-muted-foreground">#{{ server.number }}</span>
              {{ server.label }}
            </DialogTitle>
            <DialogDescription
              class="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs"
            >
              <span
                class="rounded border border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.12)] px-1.5 py-0.5 font-mono text-[0.6rem] font-semibold uppercase tracking-[0.12em] text-[hsl(var(--tac-amber))]"
              >
                {{ modeName }}
              </span>
              <span>{{ mapLabel(server.map) }}</span>
              <span
                v-if="server.regionName"
                class="inline-flex items-center gap-1"
              >
                <MapPin class="h-3 w-3" />
                {{ server.regionName }}
              </span>
              <span
                class="inline-flex items-center gap-1 font-mono tabular-nums"
              >
                <Users class="h-3 w-3" />
                {{ server.players }}/{{ server.capacity }}
              </span>
            </DialogDescription>
          </div>
        </div>

        <div class="space-y-4 p-5">
          <div class="h-1 w-full overflow-hidden rounded-full bg-muted/60">
            <div
              class="h-full rounded-full transition-all duration-500"
              :class="
                capacityPercent >= 90
                  ? 'bg-red-400'
                  : capacityPercent >= 60
                    ? 'bg-amber-400'
                    : 'bg-emerald-400'
              "
              :style="{ width: `${capacityPercent}%` }"
            />
          </div>

          <div class="flex flex-wrap items-center justify-between gap-2">
            <code
              class="rounded border border-border/60 bg-muted/40 px-2 py-1 font-mono text-xs"
              dir="ltr"
              >{{ server.connection_string }}</code
            >
            <div class="flex items-center gap-2">
              <QuickServerConnect :server="server" highlight />
              <Button
                as-child
                variant="outline"
                size="icon"
                :title="$t('pages.public_servers.details.button')"
              >
                <NuxtLink :to="`/public-servers/${server.id}`">
                  <ArrowUpRight class="h-4 w-4" />
                </NuxtLink>
              </Button>
            </div>
          </div>

          <div class="rounded-lg border border-border/60">
            <div
              class="grid grid-cols-[1fr_auto] gap-3 border-b border-border/60 px-3 py-2 font-mono text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-muted-foreground"
            >
              <span>{{ $t("pages.servers.dialog.player") }}</span>
              <span>{{ $t("pages.servers.dialog.rank") }}</span>
            </div>

            <div class="max-h-72 overflow-y-auto">
              <div v-if="loading" class="space-y-2 p-3">
                <Skeleton v-for="i in 4" :key="i" class="h-8 w-full" />
              </div>

              <p
                v-else-if="players.length === 0"
                class="px-3 py-6 text-center text-sm text-muted-foreground"
              >
                {{
                  server.players > 0
                    ? $t("pages.servers.dialog.players_unknown")
                    : $t("pages.servers.dialog.empty")
                }}
              </p>

              <div
                v-for="player in players"
                v-else
                :key="player.steam_id"
                class="grid grid-cols-[1fr_auto] items-center gap-3 border-b border-border/40 px-3 py-2 last:border-b-0"
              >
                <NuxtLink
                  :to="`/players/${player.steam_id}`"
                  class="flex min-w-0 items-center gap-2.5 hover:text-[hsl(var(--tac-amber))]"
                  :class="{ 'pointer-events-none': !player.registered }"
                >
                  <div
                    class="h-7 w-7 shrink-0 overflow-hidden rounded-md bg-muted"
                  >
                    <img
                      v-if="player.avatar_url"
                      :src="player.avatar_url"
                      class="h-full w-full object-cover"
                      @error="onAvatarError"
                    />
                  </div>
                  <span class="truncate text-sm font-medium">
                    {{ player.name || player.steam_id }}
                  </span>
                </NuxtLink>
                <div class="flex items-center gap-2">
                  <span
                    v-if="player.points > 0"
                    class="font-mono text-xs tabular-nums text-muted-foreground"
                  >
                    {{ player.points }}
                  </span>
                  <img
                    v-if="player.skill_group"
                    :src="csRankIcon(7, player.skill_group) || undefined"
                    :alt="player.rank_name"
                    :title="player.rank_name"
                    class="h-5 w-auto"
                  />
                  <span v-else class="text-xs text-muted-foreground/60">—</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </DialogContent>
  </Dialog>
</template>
