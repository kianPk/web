<script setup lang="ts">
import ClubServerManage from "~/components/clubs/ClubServerManage.vue";

defineProps<{
  clubId: string;
}>();
</script>

<template>
  <ClubServerManage :club-id="clubId" />
</template>
