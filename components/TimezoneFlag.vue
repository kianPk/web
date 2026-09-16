<template>
  <!-- Image flags: Windows often renders emoji regional indicators as "IR" text. -->
  <img
    v-if="code"
    :src="`https://flagcdn.com/w40/${code}.png`"
    :srcset="`https://flagcdn.com/w80/${code}.png 2x`"
    :alt="code.toUpperCase()"
    :title="code.toUpperCase()"
    class="inline-block h-[0.9em] w-[1.2em] shrink-0 rounded-[1px] object-cover"
    width="20"
    height="15"
    loading="lazy"
    decoding="async"
  />
  <span v-else class="inline-block text-[0.85em] leading-none" aria-hidden="true"
    >🌍</span
  >
</template>

<script lang="ts">
export default {
  props: {
    country: {
      type: String,
      required: false,
    },
  },
  computed: {
    code(): string | null {
      const raw = (this.country || "").trim();
      if (!raw) return null;
      // ISO 3166-1 alpha-2 (e.g. IR, us)
      if (/^[a-zA-Z]{2}$/.test(raw)) {
        return raw.toLowerCase();
      }
      return null;
    },
  },
};
</script>
