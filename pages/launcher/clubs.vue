<script setup lang="ts">
import LauncherEmbed from "~/components/launcher/LauncherEmbed.vue";

definePageMeta({ layout: "launcher" });
useHead({ title: "Clubs" });
</script>

<template>
  <LauncherEmbed src="/clubs" />
</template>
