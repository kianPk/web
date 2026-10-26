<script setup lang="ts">
import PlayPage from "~/pages/play/index.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Play" });
</script>

<template>
  <div class="launcher-panel">
    <PlayPage />
  </div>
</template>
