<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = withDefaults(
  defineProps<{
    tournament: any;
    onCard?: boolean;
  }>(),
  { onCard: false },
);

const { t } = useI18n();

const club = computed(() => props.tournament?.club ?? null);
const tag = computed(
  () => club.value?.short_name || club.value?.name || "",
);
const mode = computed(() => props.tournament?.participation_mode || "");
const modeLabel = computed(() => {
  if (mode.value === "in_person") return t("tournament.participation.in_person");
  if (mode.value === "online" && club.value)
    return t("tournament.participation.online");
  return "";
});
</script>

<template>
  <span
    v-if="club || modeLabel"
    class="inline-flex max-w-full flex-wrap items-center gap-1.5"
  >
    <span
      v-if="club?.slug || club?.id"
      class="inline-flex min-w-0 cursor-pointer items-center rounded-full px-2 py-0.5 font-mono text-[0.55rem] font-bold uppercase tracking-[0.14em] text-[hsl(var(--tac-amber))] backdrop-blur-sm"
      :class="
        onCard
          ? 'border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.12)]'
          : 'bg-black/55 hover:bg-black/70'
      "
      @click.stop.prevent="$router.push(`/clubs/${club.slug || club.id}`)"
    >
      <span class="truncate">{{ tag }}</span>
    </span>
    <span
      v-if="modeLabel"
      class="inline-flex shrink-0 items-center rounded-full px-2 py-0.5 font-mono text-[0.55rem] font-bold uppercase tracking-[0.12em]"
      :class="
        onCard
          ? 'border border-border/70 bg-muted/35 text-muted-foreground'
          : 'bg-black/45 text-white/80'
      "
    >
      {{ modeLabel }}
    </span>
  </span>
</template>
