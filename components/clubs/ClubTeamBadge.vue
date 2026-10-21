<script setup lang="ts">
import { Shield } from "lucide-vue-next";
import { useI18n } from "vue-i18n";
import type { TeamClub } from "~/utilities/teamClubs";

const props = withDefaults(
  defineProps<{
    clubs?: TeamClub[];
    officialClub?: TeamClub | null;
    compact?: boolean;
  }>(),
  { clubs: () => [], officialClub: null, compact: false },
);

const { t } = useI18n();

function openClub(club: TeamClub, event: Event) {
  event.stopPropagation();
  event.preventDefault();
  void useRouter().push(`/clubs/${club.slug || club.id}`);
}

function label(club: TeamClub) {
  return club.short_name || club.name;
}
</script>

<template>
  <span
    v-if="officialClub || clubs.length"
    class="inline-flex max-w-full flex-wrap items-center gap-1.5"
  >
    <span
      v-if="officialClub"
      class="inline-flex min-w-0 max-w-full cursor-pointer items-center gap-1 rounded-sm border border-[hsl(var(--tac-amber)/0.55)] bg-[hsl(var(--tac-amber)/0.16)] px-1.5 py-0.5 font-mono text-[0.55rem] font-bold uppercase tracking-[0.14em] text-[hsl(var(--tac-amber))]"
      @click="openClub(officialClub, $event)"
    >
      <Shield class="h-3 w-3 shrink-0" />
      <span class="truncate">
        {{
          compact
            ? label(officialClub)
            : t("team.club.identity_of", { club: label(officialClub) })
        }}
      </span>
    </span>
    <span
      v-for="club in clubs"
      :key="club.id"
      class="inline-flex min-w-0 max-w-full cursor-pointer items-center rounded-full border border-border/70 bg-muted/40 px-2 py-0.5 text-[0.65rem] font-medium text-muted-foreground hover:border-[hsl(var(--tac-amber)/0.4)] hover:text-foreground"
      @click="openClub(club, $event)"
    >
      <span class="truncate">
        {{ t("team.club.member_of", { club: club.name }) }}
      </span>
    </span>
  </span>
</template>
