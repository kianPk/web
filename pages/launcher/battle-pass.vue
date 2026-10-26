<script setup lang="ts">
import LauncherEmbed from "~/components/launcher/LauncherEmbed.vue";

definePageMeta({ layout: "launcher" });
useHead({ title: "Battle Pass" });
</script>

<template>
  <LauncherEmbed src="/battle-pass" />
</template>
