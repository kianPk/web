<script setup lang="ts">
import { PlusCircle, Search } from "lucide-vue-next";
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ClubCoverCard from "~/components/clubs/ClubCoverCard.vue";
import {
  tacticalCtaButtonClasses,
  tacticalHeaderActionClasses,
} from "~/utilities/tacticalClasses";
import { OWN_CLUB_TAG } from "~/utilities/clubErrors";

const { t } = useI18n();
const auth = useAuthStore();
const me = computed(() => auth.me);
const { client } = useApolloClient();
const query = ref("");
const clubs = ref<any[]>([]);
const loading = ref(true);
const canCreateClub = ref(false);

const myClubs = computed(() =>
  clubs.value.filter((c) => (c.members || []).length > 0),
);
const discoverClubs = computed(() =>
  clubs.value.filter((c) => !(c.members || []).length),
);
const searching = computed(() => query.value.trim().length > 0);

async function load() {
  loading.value = true;
  try {
    const q = query.value.trim();
    const { data } = await client.query({
      query: gql`
        query ClubsList($where: clubs_bool_exp!, $me: bigint!) {
          clubs(
            where: $where
            order_by: [{ created_at: desc }]
            limit: 60
          ) {
            id
            name
            slug
            short_name
            avatar_url
            cover_url
            description
            join_mode
            members_aggregate {
              aggregate {
                count
              }
            }
            members(where: { steam_id: { _eq: $me } }, limit: 1) {
              steam_id
              is_primary
              role
            }
          }
        }
      `,
      variables: {
        me: me.value?.steam_id ? String(me.value.steam_id) : "0",
        where: q
          ? {
              _or: [
                { name: { _ilike: `%${q}%` } },
                { slug: { _ilike: `%${q}%` } },
                { short_name: { _ilike: `%${q}%` } },
              ],
            }
          : {},
      },
      fetchPolicy: "network-only",
    });
    clubs.value = data?.clubs ?? [];
  } catch (e) {
    console.error(e);
    clubs.value = [];
  } finally {
    loading.value = false;
  }
}

watch(query, () => {
  void load();
});
async function loadOwnClub() {
  canCreateClub.value = !!auth.isAdmin;
  if (!me.value?.steam_id) return;
  try {
    const { data } = await client.query({
      query: gql`
        query MyOwnClubTag($steam_id: bigint!, $tag: String!) {
          player_tags(
            where: { steam_id: { _eq: $steam_id }, tag: { _eq: $tag } }
            limit: 1
          ) {
            tag
          }
        }
      `,
      variables: { steam_id: String(me.value.steam_id), tag: OWN_CLUB_TAG },
      fetchPolicy: "network-only",
      context: { optional: true },
    });
    canCreateClub.value = !!auth.isAdmin || (data?.player_tags?.length ?? 0) > 0;
  } catch {
    canCreateClub.value = !!auth.isAdmin;
  }
}

onMounted(() => {
  void load();
  void loadOwnClub();
});
</script>

<template>
  <PageTransition>
    <TacticalPageHeader inline-actions>
      <template #title>{{ t("pages.clubs.title") }}</template>
      <template #actions>
        <NuxtLink
          v-if="me && canCreateClub"
          to="/clubs/create"
          :class="[
            tacticalCtaButtonClasses,
            tacticalHeaderActionClasses,
            'max-md:aspect-square max-md:!px-0',
          ]"
        >
          <PlusCircle class="h-4 w-4" />
          <span class="hidden md:inline">{{ t("pages.clubs.create") }}</span>
        </NuxtLink>
      </template>
    </TacticalPageHeader>
  </PageTransition>

  <PageTransition :delay="80" class="mt-6">
    <div class="relative max-w-md">
      <Search
        class="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground"
      />
      <input
        v-model="query"
        type="search"
        :placeholder="t('pages.clubs.search')"
        class="h-10 w-full rounded-lg border border-border bg-card/70 pl-9 pr-3 text-sm outline-none transition focus:border-[hsl(var(--tac-amber)/0.5)]"
      />
    </div>
  </PageTransition>

  <PageTransition :delay="120" class="mt-8">
    <div v-if="loading" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <Skeleton v-for="i in 6" :key="i" class="h-44 rounded-xl" />
    </div>

    <Empty v-else-if="!clubs.length">
      <EmptyTitle>{{ t("pages.clubs.empty_title") }}</EmptyTitle>
      <EmptyDescription>{{ t("pages.clubs.empty_desc") }}</EmptyDescription>
    </Empty>

    <div v-else class="space-y-10">
      <section v-if="!searching && myClubs.length">
        <h2 class="mb-4 font-sans text-sm font-semibold tracking-wide text-muted-foreground">
          {{ t("pages.clubs.my_clubs") }}
        </h2>
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <NuxtLink
            v-for="club in myClubs"
            :key="club.id"
            :to="`/clubs/${club.slug || club.id}`"
          >
            <ClubCoverCard
              :name="club.name"
              :short-name="club.short_name"
              :avatar-url="club.avatar_url"
              :cover-url="club.cover_url"
              :member-count="club.members_aggregate?.aggregate?.count ?? 0"
              :join-mode="t(`pages.clubs.join_modes.${club.join_mode}`)"
              :member="true"
              :primary="!!club.members?.[0]?.is_primary"
            />
          </NuxtLink>
        </div>
      </section>

      <section>
        <h2
          v-if="!searching && myClubs.length && discoverClubs.length"
          class="mb-4 font-sans text-sm font-semibold tracking-wide text-muted-foreground"
        >
          {{ t("pages.clubs.discover") }}
        </h2>
        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <NuxtLink
            v-for="club in searching ? clubs : discoverClubs"
            :key="club.id"
            :to="`/clubs/${club.slug || club.id}`"
          >
            <ClubCoverCard
              :name="club.name"
              :short-name="club.short_name"
              :avatar-url="club.avatar_url"
              :cover-url="club.cover_url"
              :member-count="club.members_aggregate?.aggregate?.count ?? 0"
              :join-mode="t(`pages.clubs.join_modes.${club.join_mode}`)"
              :member="(club.members || []).length > 0"
              :primary="!!club.members?.[0]?.is_primary"
            />
          </NuxtLink>
        </div>
      </section>
    </div>
  </PageTransition>
</template>
