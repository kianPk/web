<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import { resolveAvatarUrl } from "~/utilities/avatarUrl";

const props = defineProps<{ steamId: string | number | bigint }>();
const { client } = useApolloClient();
const apiDomain = useRuntimeConfig().public.apiDomain as string;
const club = ref<{
  id: string;
  slug: string;
  short_name?: string | null;
  name: string;
  avatar_url?: string | null;
} | null>(null);
const emblemSrc = computed(() =>
  resolveAvatarUrl(club.value?.avatar_url, apiDomain),
);

async function load() {
  if (!props.steamId) {
    club.value = null;
    return;
  }
  try {
    const { data } = await client.query({
      query: gql`
        query PrimaryClubEmblem($steam_id: bigint!) {
          club_members(
            where: { steam_id: { _eq: $steam_id }, is_primary: { _eq: true } }
            limit: 1
          ) {
            club {
              id
              slug
              short_name
              name
              avatar_url
            }
          }
        }
      `,
      variables: { steam_id: String(props.steamId) },
      fetchPolicy: "cache-first",
    });
    club.value = data?.club_members?.[0]?.club ?? null;
  } catch {
    club.value = null;
  }
}

watch(() => props.steamId, () => void load(), { immediate: true });
</script>

<template>
  <NuxtLink
    v-if="club"
    :to="`/clubs/${club.slug || club.id}`"
    class="inline-flex h-4 max-w-[4.5rem] items-center gap-0.5 overflow-hidden border border-[hsl(var(--tac-amber)/0.55)] bg-[hsl(var(--tac-amber)/0.12)] px-0.5 text-[0.55rem] font-bold uppercase leading-none text-[hsl(var(--tac-amber))]"
    :title="club.name"
    @click.stop
  >
    <img
      v-if="emblemSrc"
      :src="emblemSrc"
      alt=""
      class="h-3 w-3 shrink-0 object-cover"
    />
    <span class="truncate">{{ club.short_name || club.name.slice(0, 4) }}</span>
  </NuxtLink>
</template>
