<script setup lang="ts">
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Settings2, Users, MapPin, ArrowUpRight } from "lucide-vue-next";
import { AnimatedCard } from "@/components/ui/animated-card";
import QuickServerConnect from "~/components/match/QuickServerConnect.vue";
import cleanMapName from "~/utilities/cleanMapName";

defineProps<{
  server: Record<string, any>;
  mapName: string;
  mapPatch?: string;
  players: number;
  canManage?: boolean;
  showMode?: boolean;
}>();

const emit = defineEmits<{
  imgError: [e: Event];
}>();

function capacityPercent(players: number, max: number): number {
  return max > 0 ? Math.min(100, Math.round((players / max) * 100)) : 0;
}

function capacityTone(pct: number): {
  text: string;
  bar: string;
  glow: string;
} {
  if (pct >= 90)
    return {
      text: "text-red-400",
      bar: "bg-red-400",
      glow: "shadow-[0_0_12px_hsl(0_84%_60%/0.35)]",
    };
  if (pct >= 60)
    return {
      text: "text-amber-400",
      bar: "bg-amber-400",
      glow: "shadow-[0_0_12px_hsl(38_92%_50%/0.3)]",
    };
  return {
    text: "text-emerald-400",
    bar: "bg-emerald-400",
    glow: "shadow-[0_0_12px_hsl(152_76%_40%/0.28)]",
  };
}
</script>

<template>
  <AnimatedCard
    variant="elevated"
    class="public-server-card group relative overflow-hidden border-border/60 bg-card/80 p-0 backdrop-blur-sm transition-all duration-500 hover:-translate-y-0.5 hover:border-[hsl(var(--tac-amber)/0.45)]"
  >
    <!-- Ambient edge light on hover -->
    <div
      aria-hidden="true"
      class="pointer-events-none absolute inset-0 opacity-0 transition-opacity duration-500 group-hover:opacity-100"
      style="
        background: radial-gradient(
          120% 80% at 50% -10%,
          hsl(var(--tac-amber) / 0.12),
          transparent 55%
        );
      "
    />

    <!-- Map hero -->
    <div class="relative h-44 overflow-hidden sm:h-48">
      <img
        :src="`/img/maps/screenshots/${mapName}.webp`"
        :alt="mapName"
        class="h-full w-full object-cover transition-transform duration-700 ease-out group-hover:scale-[1.06]"
        @error="emit('imgError', $event)"
      />
      <div
        class="absolute inset-0 bg-gradient-to-t from-black via-black/55 to-black/10"
      />
      <div
        class="absolute inset-0 bg-[linear-gradient(90deg,transparent_0%,hsl(var(--tac-amber)/0.08)_50%,transparent_100%)] opacity-0 transition-opacity duration-500 group-hover:opacity-100"
      />

      <!-- Live pulse -->
      <div
        class="absolute left-3 top-3 inline-flex items-center gap-1.5 rounded-md border border-white/15 bg-black/45 px-2 py-1 backdrop-blur-md"
      >
        <span class="relative flex h-1.5 w-1.5">
          <span
            class="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-60"
          />
          <span
            class="relative inline-flex h-1.5 w-1.5 rounded-full bg-emerald-400"
          />
        </span>
        <span
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-white/90"
        >
          {{ cleanMapName(mapName) }}
        </span>
      </div>

      <div class="absolute right-3 top-3 flex flex-col items-end gap-1.5">
        <Badge
          variant="secondary"
          class="border border-white/10 bg-black/50 text-[0.65rem] uppercase tracking-wider text-white backdrop-blur-md"
        >
          {{ server.type }}
        </Badge>
        <span
          class="inline-flex items-center gap-1 rounded-md border border-white/10 bg-black/40 px-1.5 py-0.5 text-[0.65rem] text-white/75 backdrop-blur-md"
        >
          <MapPin class="h-3 w-3 opacity-70" />
          {{ server.region }}
        </span>
      </div>

      <!-- Map patch watermark -->
      <div
        v-if="mapPatch"
        class="pointer-events-none absolute inset-0 flex items-center justify-center"
      >
        <img
          :src="mapPatch"
          class="h-auto max-h-[55%] w-1/3 max-w-[88px] object-contain opacity-70 drop-shadow-2xl transition-transform duration-700 group-hover:scale-110"
        />
      </div>

      <!-- Title over hero -->
      <div class="absolute inset-x-0 bottom-0 space-y-1.5 p-4">
        <div class="flex items-end justify-between gap-2">
          <div class="min-w-0">
            <p
              class="truncate font-sans text-lg font-bold leading-tight tracking-tight text-white drop-shadow-md"
            >
              {{ server.label }}
            </p>
            <p
              v-if="showMode && server.game_mode"
              class="mt-1 inline-flex max-w-full truncate rounded border border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.12)] px-1.5 py-0.5 font-mono text-[0.6rem] font-semibold uppercase tracking-[0.12em] text-[hsl(var(--tac-amber))]"
            >
              {{ server.game_mode.name }}
            </p>
          </div>
        </div>
      </div>
    </div>

    <!-- Body -->
    <div class="relative space-y-3 px-4 pb-4 pt-3">
      <div class="flex items-center justify-between gap-3">
        <span
          class="inline-flex items-center gap-1.5 text-xs font-medium uppercase tracking-[0.12em] text-muted-foreground"
        >
          <Users class="h-3.5 w-3.5 opacity-70" />
          {{ $t("pages.public_servers.players") }}
        </span>
        <span
          class="font-mono text-sm font-semibold tabular-nums"
          :class="
            capacityTone(capacityPercent(players, server.max_players)).text
          "
        >
          {{ players }}
          <span class="text-muted-foreground/70">/</span>
          {{ server.max_players }}
        </span>
      </div>

      <div
        class="relative h-1.5 w-full overflow-hidden rounded-full bg-muted/60"
      >
        <div
          class="h-full rounded-full transition-all duration-500"
          :class="[
            capacityTone(capacityPercent(players, server.max_players)).bar,
            capacityTone(capacityPercent(players, server.max_players)).glow,
          ]"
          :style="`width: ${capacityPercent(players, server.max_players)}%`"
        />
      </div>

      <div class="flex items-center gap-2 pt-1">
        <div class="min-w-0 flex-1 [&>div]:w-full [&_a]:flex-1 [&_a_button]:w-full">
          <QuickServerConnect :server="server" highlight />
        </div>
        <Button
          as-child
          variant="outline"
          size="icon"
          class="shrink-0 border-border/70 bg-background/40 hover:border-[hsl(var(--tac-amber)/0.5)] hover:bg-[hsl(var(--tac-amber)/0.08)]"
          :title="$t('pages.public_servers.details.button')"
        >
          <NuxtLink
            :to="`/public-servers/${server.id}`"
            :aria-label="$t('pages.public_servers.details.button')"
          >
            <ArrowUpRight class="h-4 w-4 opacity-80" />
          </NuxtLink>
        </Button>
        <Button
          v-if="canManage"
          as-child
          variant="outline"
          size="icon"
          class="shrink-0 border-border/70 bg-background/40"
          :title="$t('pages.public_servers.manage')"
        >
          <NuxtLink
            :to="`/dedicated-servers/${server.id}`"
            :aria-label="$t('pages.public_servers.manage')"
          >
            <Settings2 class="h-4 w-4" />
          </NuxtLink>
        </Button>
      </div>
    </div>
  </AnimatedCard>
</template>
