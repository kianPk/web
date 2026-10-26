<script setup lang="ts">
import ClubsView from "~/components/launcher/views/ClubsView.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Clubs" });
</script>

<template>
  <div class="launcher-panel">
    <ClubsView />
  </div>
</template>
