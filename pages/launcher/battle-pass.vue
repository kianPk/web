<script setup lang="ts">
import BattlePassView from "~/components/launcher/views/BattlePassView.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Battle Pass" });
</script>

<template>
  <div class="launcher-panel">
    <BattlePassView />
  </div>
</template>
