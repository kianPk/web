<script setup lang="ts">
import LauncherEmbed from "~/components/launcher/LauncherEmbed.vue";

definePageMeta({ layout: "launcher" });
useHead({ title: "Scrims" });
</script>

<template>
  <LauncherEmbed src="/scrims" />
</template>
