<script setup lang="ts">
import LauncherEmbed from "~/components/launcher/LauncherEmbed.vue";

definePageMeta({ layout: "launcher" });
useHead({ title: "Servers" });
</script>

<template>
  <LauncherEmbed src="/game-servers" />
</template>
