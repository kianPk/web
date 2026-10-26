<script setup lang="ts">
import ScrimsPage from "~/pages/scrims/index.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Scrims" });
</script>

<template>
  <div class="launcher-panel">
    <ScrimsPage />
  </div>
</template>
