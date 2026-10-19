<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";

const { t } = useI18n();
const { client } = useApolloClient();
const auth = useAuthStore();
const parties = ref<any[]>([]);
const loading = ref(true);

async function load() {
  loading.value = true;
  try {
    const { data } = await client.query({
      query: gql`
        query ClubPartyFinder {
          club_parties(
            where: { listed: { _eq: true } }
            order_by: [{ created_at: desc }]
            limit: 60
          ) {
            id
            title
            min_elo
            max_elo
            club {
              id
              name
              slug
              short_name
            }
            leader {
              name
              steam_id
            }
            members {
              steam_id
              is_guest
              player {
                name
              }
            }
          }
        }
      `,
      fetchPolicy: "network-only",
    });
    parties.value = data?.club_parties ?? [];
  } catch {
    parties.value = [];
  } finally {
    loading.value = false;
  }
}

async function joinAsGuest(partyId: string) {
  if (!auth.me) {
    await navigateTo("/login");
    return;
  }
  await client.mutate({
    mutation: gql`
      mutation($object: club_party_members_insert_input!) {
        insert_club_party_members_one(object: $object) {
          party_id
        }
      }
    `,
    variables: {
      object: {
        party_id: partyId,
        steam_id: String(auth.me.steam_id),
        is_guest: true,
      },
    },
  });
  await load();
}

onMounted(() => void load());
</script>

<template>
  <PageTransition>
    <TacticalPageHeader>
      <template #title>{{ t("pages.clubs.party_finder") }}</template>
    </TacticalPageHeader>
  </PageTransition>
  <PageTransition :delay="80" class="mt-6">
    <div v-if="loading" class="text-sm text-muted-foreground">
      {{ t("common.loading") }}
    </div>
    <Empty v-else-if="!parties.length">
      <EmptyTitle>{{ t("pages.clubs.party_finder_empty") }}</EmptyTitle>
      <EmptyDescription>{{ t("pages.clubs.party_finder_empty_desc") }}</EmptyDescription>
    </Empty>
    <div v-else class="grid gap-3">
      <div
        v-for="p in parties"
        :key="p.id"
        class="flex flex-wrap items-center justify-between gap-3 border border-border bg-card/40 p-4"
      >
        <div>
          <NuxtLink
            :to="`/clubs/${p.club?.slug || p.club?.id}`"
            class="font-bold uppercase hover:text-[hsl(var(--tac-amber))]"
          >
            {{ p.club?.name }}
          </NuxtLink>
          <div class="text-sm">{{ p.title || t("pages.clubs.party") }}</div>
          <div class="mt-1 text-xs text-muted-foreground">
            {{ t("pages.clubs.leader") }}: {{ p.leader?.name }} ·
            {{ (p.members || []).length }} players
            <span v-if="p.min_elo"> · Elo ≥ {{ p.min_elo }}</span>
          </div>
        </div>
        <Button size="sm" variant="outline" @click="joinAsGuest(p.id)">
          {{ t("pages.clubs.join_as_guest") }}
        </Button>
      </div>
    </div>
  </PageTransition>
</template>
