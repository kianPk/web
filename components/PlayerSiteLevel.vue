<script lang="ts" setup>
import { useI18n } from "vue-i18n";

const { t } = useI18n();
const props = defineProps<{
  level?: number | null;
  xp?: number | null;
  steamId?: string | null;
  size?: "sm" | "md" | "lg";
  refreshKey?: number;
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

const numberStyle = computed(() => ({
  color: levelColor.value,
  textShadow: `0 0 8px color-mix(in srgb, ${levelColor.value} 40%, transparent)`,
}));

const sizeClass = computed(() => {
  if (props.size === "lg") return "h-14 w-14";
  if (props.size === "md") return "h-8 w-8";
  return "h-[18px] w-[18px]";
});

const textClass = computed(() => {
  if (props.size === "lg") return "text-lg";
  if (props.size === "md") return "text-xs";
  return "text-[0.62rem]";
});

const insetClass = computed(() =>
  props.size === "lg" ? "inset-[3px]" : "inset-[2px]",
);

watch(
  () => [props.steamId, props.refreshKey] as const,
  async ([steamId]) => {
    fetched.value = null;
    if (!steamId || props.level != null) return;
    try {
      const apiDomain = useRuntimeConfig().public.apiDomain as string;
      const data = await $fetch<{
        players: Array<{ steam_id: string; xp: number; level: number }>;
      }>(
        `https://${apiDomain}/hosted-servers/site-xp/lookup?steam_ids=${encodeURIComponent(String(steamId))}&_=${props.refreshKey || 0}`,
        { cache: "no-store" },
      );
      const row = data?.players?.[0];
      fetched.value = row
        ? { level: row.level, xp: row.xp }
        : { level: 1, xp: 0 };
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
    class="relative inline-grid shrink-0 place-items-center rounded-full align-middle"
    :class="sizeClass"
    :style="badgeStyle"
    :title="t('pages.players.detail.site_level_title', { level: shown.level, xp: shown.xp })"
  >
    <span class="absolute rounded-full bg-[hsl(240_8%_10%)]" :class="insetClass"></span>
    <span
      class="relative font-mono font-bold tabular-nums leading-none"
      :class="textClass"
      :style="numberStyle"
    >{{ shown.level }}</span>
  </span>
</template>
