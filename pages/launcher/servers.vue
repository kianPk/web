<script setup lang="ts">
import ServersPage from "~/pages/game-servers/index.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Servers" });
</script>

<template>
  <div class="launcher-panel">
    <ServersPage />
  </div>
</template>
