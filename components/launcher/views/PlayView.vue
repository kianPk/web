<script setup lang="ts">
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import PlayMatchmaking from "~/components/play/PlayMatchmaking.vue";
import PlaySchedule from "~/components/play/PlaySchedule.vue";
import PlayDraftRooms from "~/components/play/PlayDraftRooms.vue";
import { tacticalSectionSeparatorClasses } from "~/utilities/tacticalClasses";

// Matchmaking and the schedule render nothing when they have nothing to
// offer, so the separator rule only ever sits between sections that are
// actually shown. Draft rooms always render (the host bar), so they lead.
const sectionClasses = ["mt-8", "md:mt-10", tacticalSectionSeparatorClasses];
const route = useRoute();
const embedSpaced = computed(
  () => route.query.embed === "1" || route.query.embed === "true",
);
</script>

<template>
  <div
    class="flex flex-col"
    :class="embedSpaced ? 'gap-10 sm:gap-12' : 'gap-0'"
  >
    <PageTransition>
      <PlayDraftRooms />
    </PageTransition>

    <PageTransition :delay="50">
      <PlayMatchmaking
        :class="embedSpaced ? undefined : sectionClasses"
      />
    </PageTransition>

    <PageTransition :delay="100">
      <PlaySchedule :class="embedSpaced ? undefined : sectionClasses" />
    </PageTransition>
  </div>
</template>
