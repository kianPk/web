<script setup lang="ts">
import ScrimsView from "~/components/launcher/views/ScrimsView.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Scrims" });
</script>

<template>
  <div class="launcher-panel">
    <ScrimsView />
  </div>
</template>
