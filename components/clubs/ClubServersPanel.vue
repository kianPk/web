<script setup lang="ts">
import ClubPublicServers from "~/components/clubs/ClubPublicServers.vue";
import ClubServerManage from "~/components/clubs/ClubServerManage.vue";

defineProps<{
  clubId: string;
  canManage: boolean;
}>();
</script>

<template>
  <ClubServerManage v-if="canManage" :club-id="clubId" />
  <ClubPublicServers v-else :club-id="clubId" />
</template>
