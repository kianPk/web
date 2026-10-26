<script setup lang="ts">
import LauncherEmbed from "~/components/launcher/LauncherEmbed.vue";

definePageMeta({ layout: "launcher" });
useHead({ title: "Inventory" });
</script>

<template>
  <LauncherEmbed src="/apps/inventory" />
</template>
