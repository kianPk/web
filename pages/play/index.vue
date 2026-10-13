<script setup lang="ts">
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import PlayMatchmaking from "~/components/play/PlayMatchmaking.vue";
import PlaySchedule from "~/components/play/PlaySchedule.vue";
import PlayDraftRooms from "~/components/play/PlayDraftRooms.vue";
import { tacticalSectionSeparatorClasses } from "~/utilities/tacticalClasses";

// Matchmaking and the schedule render nothing when they have nothing to
// offer, so the separator rule only ever sits between sections that are
// actually shown. Draft rooms always render (the host bar), so they lead.
const sectionClasses = ["mt-8", tacticalSectionSeparatorClasses];
</script>

<template>
  <PageTransition>
    <PlayDraftRooms />
  </PageTransition>

  <PageTransition :delay="50">
    <PlayMatchmaking :class="sectionClasses" />
  </PageTransition>

  <PageTransition :delay="100">
    <PlaySchedule :class="sectionClasses" />
  </PageTransition>
</template>
