<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TimeAgo from "./TimeAgo.vue";
import { Button } from "~/components/ui/button";

const props = defineProps<{
  invite: {
    id: string;
    created_at: string;
    club: { id: string; name: string; slug?: string | null };
    invited_by: { name: string };
  };
}>();

const emit = defineEmits<{ (e: "resolved"): void }>();
const { t } = useI18n();
const { client } = useApolloClient();
const busy = ref(false);

async function accept() {
  busy.value = true;
  try {
    await client.mutate({
      mutation: gql`
        mutation AcceptClubInvite($invite_id: uuid!) {
          acceptClubInvite(invite_id: $invite_id) {
            success
          }
        }
      `,
      variables: { invite_id: props.invite.id },
    });
    emit("resolved");
    await navigateTo(`/clubs/${props.invite.club.slug || props.invite.club.id}`);
  } finally {
    busy.value = false;
  }
}

async function deny() {
  busy.value = true;
  try {
    await client.mutate({
      mutation: gql`
        mutation DenyClubInvite($id: uuid!) {
          delete_club_invites_by_pk(id: $id) {
            id
          }
        }
      `,
      variables: { id: props.invite.id },
    });
    emit("resolved");
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div>
    <h3 class="mb-2 text-lg font-semibold">{{ t("pages.clubs.invite.title") }}</h3>
    <p>
      {{ t("pages.clubs.invite.message", { club: invite.club.name }) }}
    </p>
    <div class="mt-3 flex justify-between space-x-2">
      <p class="mb-2 text-sm text-muted-foreground">
        {{ t("pages.clubs.invite.invited_by", { name: invite.invited_by.name }) }}
        <TimeAgo :date="invite.created_at" class="text-xs" />
      </p>
      <div class="flex gap-2">
        <Button variant="outline" :disabled="busy" @click="deny">
          {{ t("pages.clubs.invite.deny") }}
        </Button>
        <Button variant="default" :disabled="busy" @click="accept">
          {{ t("pages.clubs.invite.accept") }}
        </Button>
      </div>
    </div>
  </div>
</template>
