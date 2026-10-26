<script setup lang="ts">
import ServersView from "~/components/launcher/views/ServersView.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Servers" });
</script>

<template>
  <div class="launcher-panel">
    <ServersView />
  </div>
</template>
