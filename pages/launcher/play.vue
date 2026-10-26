<script setup lang="ts">
import LauncherEmbed from "~/components/launcher/LauncherEmbed.vue";

definePageMeta({ layout: "launcher" });
useHead({ title: "Play" });
</script>

<template>
  <LauncherEmbed src="/play" />
</template>
