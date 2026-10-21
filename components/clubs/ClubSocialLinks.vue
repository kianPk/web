<script setup lang="ts">
defineProps<{
  telegram?: string | null;
  instagram?: string | null;
}>();
</script>

<template>
  <div v-if="telegram || instagram" class="flex flex-wrap items-center gap-2">
    <a
      v-if="telegram"
      :href="telegram"
      target="_blank"
      rel="noopener noreferrer"
      class="inline-flex items-center gap-1.5 rounded-full border border-border/70 bg-card/60 px-2.5 py-1 text-xs text-muted-foreground transition-colors hover:border-[hsl(var(--tac-amber)/0.5)] hover:text-foreground"
    >
      <svg viewBox="0 0 24 24" class="h-3.5 w-3.5 fill-current" aria-hidden="true">
        <path
          d="M21.5 4.3 18.6 20c-.2 1-.8 1.2-1.6.8l-4.5-3.3-2.2 2.1c-.2.3-.5.5-.9.5l.3-4.7 8.5-7.7c.4-.3 0-.5-.5-.2l-10.5 6.6-4.5-1.4c-1-.3-1-.9.2-1.4L20.2 3.5c.8-.3 1.5.2 1.3.8Z"
        />
      </svg>
      {{ $t("pages.clubs.telegram") }}
    </a>
    <a
      v-if="instagram"
      :href="instagram"
      target="_blank"
      rel="noopener noreferrer"
      class="inline-flex items-center gap-1.5 rounded-full border border-border/70 bg-card/60 px-2.5 py-1 text-xs text-muted-foreground transition-colors hover:border-[hsl(var(--tac-amber)/0.5)] hover:text-foreground"
    >
      <svg viewBox="0 0 24 24" class="h-3.5 w-3.5 fill-current" aria-hidden="true">
        <path
          d="M7 3h10a4 4 0 0 1 4 4v10a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V7a4 4 0 0 1 4-4Zm10 2H7a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2Zm-5 3.2A3.8 3.8 0 1 1 8.2 12 3.8 3.8 0 0 1 12 8.2Zm0 2A1.8 1.8 0 1 0 13.8 12 1.8 1.8 0 0 0 12 10.2ZM17.2 6.6a.9.9 0 1 1-.9.9.9.9 0 0 1 .9-.9Z"
        />
      </svg>
      {{ $t("pages.clubs.instagram") }}
    </a>
  </div>
</template>
