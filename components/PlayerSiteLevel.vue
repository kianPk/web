<script lang="ts" setup>
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const props = defineProps<{
  level?: number | null;
  xp?: number | null;
  steamId?: string | null;
}>();

const fetched = ref<{ level: number; xp: number } | null>(null);

const LEVEL_COLORS: { max: number; color: string }[] = [
  { max: 1, color: "hsl(0 0% 88%)" },
  { max: 3, color: "hsl(125 90% 45%)" },
  { max: 7, color: "hsl(45 100% 50%)" },
  { max: 9, color: "hsl(22 100% 52%)" },
  { max: 10, color: "hsl(8 95% 50%)" },
];

const shown = computed(() => {
  if (props.level != null) {
    return {
      level: Math.min(10, Math.max(1, Math.round(props.level))),
      xp: Math.max(0, Math.round(props.xp ?? 0)),
    };
  }
  return fetched.value;
});

const levelColor = computed(() => {
  const level = shown.value?.level ?? 1;
  return LEVEL_COLORS.find((band) => level <= band.max)?.color ?? LEVEL_COLORS[0].color;
});

const badgeStyle = computed(() => {
  const level = shown.value?.level ?? 1;
  const deg = Math.max(0, Math.min(10, level)) * 36;
  return {
    background: `conic-gradient(${levelColor.value} ${deg}deg, color-mix(in srgb, ${levelColor.value} 22%, transparent) ${deg}deg)`,
  };
});

watch(
  () => props.steamId,
  async (steamId) => {
    fetched.value = null;
    if (!steamId || props.level != null) return;
    try {
      const apiDomain = useRuntimeConfig().public.apiDomain as string;
      const data = await $fetch<{
        players: Array<{ steam_id: string; xp: number; level: number }>;
      }>(`https://${apiDomain}/hosted-servers/site-xp?steam_ids=${steamId}`);
      const row = data?.players?.[0];
      if (row) fetched.value = { level: row.level, xp: row.xp };
    } catch {
      fetched.value = { level: 1, xp: 0 };
    }
  },
  { immediate: true },
);
</script>

<template>
  <span
    v-if="shown"
    class="inline-flex h-[26px] items-center gap-1.5 rounded border px-[0.6rem] leading-none"
    :style="{
      borderColor: `color-mix(in srgb, ${levelColor} 40%, transparent)`,
      backgroundColor: 'hsl(var(--card) / 0.55)',
    }"
    :title="t('pages.players.detail.site_level_title', { level: shown.level, xp: shown.xp })"
  >
    <span
      class="grid h-4 w-4 place-items-center rounded-full"
      :style="badgeStyle"
    >
      <span
        class="grid h-3 w-3 place-items-center rounded-full bg-card font-mono text-[0.55rem] font-bold"
        :style="{ color: levelColor }"
      >
        {{ shown.level }}
      </span>
    </span>
    <span class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.14em] text-muted-foreground">
      {{ t("pages.players.detail.site_level") }}
    </span>
  </span>
</template>
