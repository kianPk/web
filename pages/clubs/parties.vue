<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import ClubAvatar from "~/components/clubs/ClubAvatar.vue";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

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
              avatar_url
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
                avatar_url
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
    <div v-else class="grid gap-3 sm:grid-cols-2">
      <div
        v-for="p in parties"
        :key="p.id"
        class="flex flex-col gap-3 rounded-xl border border-border/70 bg-card/40 p-4"
      >
        <div class="flex items-center gap-3">
          <ClubAvatar
            :name="p.club?.name || '?'"
            :short-name="p.club?.short_name"
            :src="p.club?.avatar_url"
            size="sm"
            :ring="false"
          />
          <div class="min-w-0">
            <NuxtLink
              :to="`/clubs/${p.club?.slug || p.club?.id}`"
              class="truncate font-semibold hover:text-[hsl(var(--tac-amber))]"
            >
              {{ p.club?.name }}
            </NuxtLink>
            <div class="text-sm text-muted-foreground">
              {{ p.title || t("pages.clubs.party") }}
            </div>
          </div>
        </div>
        <div class="flex -space-x-2">
          <div
            v-for="i in 5"
            :key="i"
            class="flex h-8 w-8 items-center justify-center overflow-hidden rounded-full border-2 border-background bg-muted text-[0.6rem] text-muted-foreground"
          >
            <img
              v-if="p.members?.[i - 1]?.player?.avatar_url"
              :src="p.members[i - 1].player.avatar_url"
              alt=""
              class="h-full w-full object-cover"
            />
            <span v-else-if="p.members?.[i - 1]">
              {{ (p.members[i - 1].player?.name || "?").slice(0, 1) }}
            </span>
          </div>
        </div>
        <div class="flex items-center justify-between gap-2">
          <div class="text-xs text-muted-foreground">
            {{ t("pages.clubs.leader") }}: {{ p.leader?.name }}
            <span v-if="p.min_elo"> · Elo ≥ {{ p.min_elo }}</span>
          </div>
          <Button size="sm" :class="tacticalCtaButtonClasses" @click="joinAsGuest(p.id)">
            {{ t("pages.clubs.join_as_guest") }}
          </Button>
        </div>
      </div>
    </div>
  </PageTransition>
</template>
