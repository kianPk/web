<script setup lang="ts">
import GamenetView from "~/components/launcher/views/GamenetView.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Gamenet" });
</script>

<template>
  <div class="launcher-panel">
    <GamenetView />
  </div>
</template>
