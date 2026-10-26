<script setup lang="ts">
import PlayView from "~/components/launcher/views/PlayView.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Play" });
</script>

<template>
  <div class="launcher-panel">
    <PlayView />
  </div>
</template>
