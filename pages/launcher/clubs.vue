<script setup lang="ts">
import ClubsPage from "~/pages/clubs/index.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Clubs" });
</script>

<template>
  <div class="launcher-panel">
    <ClubsPage />
  </div>
</template>
