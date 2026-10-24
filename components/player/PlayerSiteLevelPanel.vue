<script lang="ts" setup>
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import PlayerSiteLevel from "~/components/PlayerSiteLevel.vue";

const props = defineProps<{
  steamId: string;
  refreshKey?: number;
}>();

const { t } = useI18n();

const FLOORS = [0, 200, 500, 1000, 1500, 2500, 4000, 6000, 8500, 11500];

const row = ref<{ level: number; xp: number } | null>(null);

watch(
  () => [props.steamId, props.refreshKey] as const,
  async ([steamId]) => {
    row.value = null;
    if (!steamId) return;
    try {
      const apiDomain = useRuntimeConfig().public.apiDomain as string;
      const data = await $fetch<{
        players: Array<{ steam_id: string; xp: number; level: number }>;
      }>(
        `https://${apiDomain}/hosted-servers/site-xp/lookup?steam_ids=${encodeURIComponent(String(steamId))}&_=${props.refreshKey || 0}`,
        { cache: "no-store" },
      );
      const found = data?.players?.[0];
      row.value = found
        ? { level: found.level, xp: found.xp }
        : { level: 1, xp: 0 };
    } catch {
      row.value = { level: 1, xp: 0 };
    }
  },
  { immediate: true },
);

const progress = computed(() => {
  const current = row.value;
  if (!current) return 0;
  if (current.level >= 10) return 100;
  const floor = FLOORS[current.level - 1] ?? 0;
  const next = FLOORS[current.level] ?? floor;
  if (next <= floor) return 100;
  return Math.max(0, Math.min(100, ((current.xp - floor) / (next - floor)) * 100));
});

const nextXp = computed(() => {
  const current = row.value;
  if (!current || current.level >= 10) return null;
  return FLOORS[current.level] ?? null;
});
</script>

<template>
  <div
    v-if="row"
    class="relative overflow-hidden rounded-xl border border-[hsl(var(--tac-amber)/0.28)] bg-[radial-gradient(120%_140%_at_100%_0%,hsl(var(--tac-amber)/0.16)_0%,transparent_46%),linear-gradient(180deg,hsl(var(--card)/0.72)_0%,hsl(var(--card)/0.35)_100%)] px-4 pb-4 pt-5"
  >
    <div class="flex items-center gap-4">
      <PlayerSiteLevel :level="row.level" :xp="row.xp" size="lg" />
      <div class="min-w-0 flex-1">
        <div
          class="font-mono text-[0.58rem] font-semibold uppercase tracking-[0.22em] text-muted-foreground"
        >
          {{ t("pages.players.detail.site_level") }}
        </div>
        <div class="mt-0.5 flex items-baseline gap-2">
          <span class="font-sans text-3xl font-black tabular-nums leading-none tracking-tight">
            {{ row.level }}
          </span>
          <span class="font-mono text-xs tabular-nums text-muted-foreground">
            {{ t("pages.players.detail.site_level_xp", { xp: row.xp }) }}
          </span>
        </div>
      </div>
    </div>
    <div class="mt-3 h-1.5 overflow-hidden rounded-full bg-foreground/10">
      <div
        class="h-full rounded-full bg-[hsl(var(--tac-amber))] transition-[width] duration-500"
        :style="{ width: `${progress}%` }"
      ></div>
    </div>
    <div
      class="mt-1.5 flex items-center justify-between font-mono text-[0.58rem] uppercase tracking-[0.14em] text-muted-foreground"
    >
      <span>{{ t("pages.players.detail.site_level_now", { level: row.level }) }}</span>
      <span v-if="nextXp != null">
        {{ t("pages.players.detail.site_level_next", { level: row.level + 1, xp: nextXp }) }}
      </span>
      <span v-else>{{ t("pages.players.detail.site_level_max") }}</span>
    </div>
  </div>
</template>
