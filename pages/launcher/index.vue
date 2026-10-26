<script setup lang="ts">
definePageMeta({ layout: "launcher" });

const route = useRoute();
const query =
  route.query.desktop === "1" || route.query.desktop === "true"
    ? { desktop: "1" }
    : {};

await navigateTo({ path: "/launcher/play", query }, { replace: true });
</script>

<template>
  <div />
</template>
