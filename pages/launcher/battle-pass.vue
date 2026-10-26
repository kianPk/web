<script setup lang="ts">
import BattlePassPage from "~/pages/battle-pass/index.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Battle Pass" });
</script>

<template>
  <div class="launcher-panel">
    <BattlePassPage />
  </div>
</template>
