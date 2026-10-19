<script setup lang="ts">
import { PlusCircle, Search, Users } from "lucide-vue-next";
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import {
  tacticalCtaButtonClasses,
  tacticalHeaderActionClasses,
} from "~/utilities/tacticalClasses";

const { t } = useI18n();
const auth = useAuthStore();
const me = computed(() => auth.me);
const { client } = useApolloClient();
const query = ref("");
const clubs = ref<any[]>([]);
const loading = ref(true);

async function load() {
  loading.value = true;
  try {
    const q = query.value.trim();
    const { data } = await client.query({
      query: gql`
        query ClubsList($where: clubs_bool_exp!) {
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
            description
            join_mode
            members_aggregate {
              aggregate {
                count
              }
            }
          }
        }
      `,
      variables: {
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
onMounted(() => {
  void load();
});
</script>

<template>
  <PageTransition>
    <TacticalPageHeader inline-actions>
      <template #title>{{ t("pages.clubs.title") }}</template>
      <template #actions>
        <NuxtLink
          to="/clubs/parties"
          :class="[tacticalHeaderActionClasses, 'text-sm text-muted-foreground']"
        >
          {{ t("pages.clubs.party_finder") }}
        </NuxtLink>
        <NuxtLink
          v-if="me"
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
        class="h-9 w-full rounded-md border border-border bg-card/60 pl-9 pr-3 text-sm outline-none focus:border-[hsl(var(--tac-amber)/0.5)]"
      />
    </div>
  </PageTransition>

  <PageTransition :delay="120" class="mt-6">
    <div v-if="loading" class="text-sm text-muted-foreground">
      {{ t("common.loading") }}
    </div>
    <Empty v-else-if="!clubs.length">
      <EmptyTitle>{{ t("pages.clubs.empty_title") }}</EmptyTitle>
      <EmptyDescription>{{ t("pages.clubs.empty_desc") }}</EmptyDescription>
    </Empty>
    <div v-else class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <NuxtLink
        v-for="club in clubs"
        :key="club.id"
        :to="`/clubs/${club.slug || club.id}`"
        class="group flex gap-4 rounded-lg border border-border bg-card/40 p-4 transition hover:border-[hsl(var(--tac-amber)/0.45)] hover:bg-card/70"
      >
        <div
          class="flex h-14 w-14 shrink-0 items-center justify-center border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.1)] text-lg font-bold uppercase text-[hsl(var(--tac-amber))]"
        >
          <img
            v-if="club.avatar_url"
            :src="club.avatar_url"
            class="h-full w-full object-cover"
            alt=""
          />
          <span v-else>{{ (club.short_name || club.name || "?").slice(0, 2) }}</span>
        </div>
        <div class="min-w-0 flex-1">
          <div class="truncate font-sans text-base font-bold uppercase tracking-wide">
            {{ club.name }}
          </div>
          <div class="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
            <Users class="h-3.5 w-3.5" />
            {{ club.members_aggregate?.aggregate?.count ?? 0 }}
            <span class="uppercase tracking-wider">{{ club.join_mode }}</span>
          </div>
          <p class="mt-2 line-clamp-2 text-xs text-muted-foreground">
            {{ club.description || t("pages.clubs.no_description") }}
          </p>
        </div>
      </NuxtLink>
    </div>
  </PageTransition>
</template>
