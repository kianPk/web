<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";

const props = withDefaults(
  defineProps<{
    at?: string | number | Date | null;
    /** Hide when the deadline has passed. */
    hideWhenPast?: boolean;
    compact?: boolean;
  }>(),
  {
    at: null,
    hideWhenPast: true,
    compact: false,
  },
);

const now = ref(Date.now());
let ticker: ReturnType<typeof setInterval> | null = null;

onMounted(() => {
  ticker = setInterval(() => {
    now.value = Date.now();
  }, 1000);
});

onBeforeUnmount(() => {
  if (ticker) clearInterval(ticker);
});

const remainingMs = computed(() => {
  if (props.at == null || props.at === "") return null;
  const at = new Date(props.at).getTime();
  if (Number.isNaN(at)) return null;
  return at - now.value;
});

const visible = computed(() => {
  if (remainingMs.value == null) return false;
  if (props.hideWhenPast && remainingMs.value <= 0) return false;
  return true;
});

const parts = computed(() => {
  const ms = Math.max(0, remainingMs.value ?? 0);
  const total = Math.floor(ms / 1000);
  const days = Math.floor(total / 86400);
  const hours = Math.floor((total % 86400) / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const seconds = total % 60;
  return { days, hours, minutes, seconds, total };
});

const label = computed(() => {
  const { days, hours, minutes, seconds } = parts.value;
  const hh = String(hours).padStart(2, "0");
  const mm = String(minutes).padStart(2, "0");
  const ss = String(seconds).padStart(2, "0");
  if (days > 0) return `${days}d ${hh}:${mm}:${ss}`;
  if (hours > 0) return `${hours}:${mm}:${ss}`;
  return `${minutes}:${ss}`;
});

const urgent = computed(
  () => parts.value.total > 0 && parts.value.total < 3600,
);
</script>

<template>
  <span
    v-if="visible"
    class="event-countdown inline-flex items-center gap-1.5 font-mono tabular-nums"
    :class="[
      compact ? 'text-xs' : 'text-sm',
      urgent ? 'text-[hsl(0_92%_68%)]' : 'text-[hsl(var(--tac-amber))]',
    ]"
    role="timer"
    :aria-label="label"
  >
    <slot name="prefix" />
    <span
      class="rounded-md border px-1.5 py-0.5 font-semibold tracking-wider"
      :class="
        urgent
          ? 'border-[hsl(0_90%_62%/0.45)] bg-[hsl(0_0%_0%/0.35)]'
          : 'border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.1)]'
      "
    >
      {{ label }}
    </span>
    <slot name="suffix" />
  </span>
</template>
