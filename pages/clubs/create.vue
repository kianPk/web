<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

import { clubErrorMessage, OWN_CLUB_TAG } from "~/utilities/clubErrors";

const { t } = useI18n();
const router = useRouter();
const { client } = useApolloClient();
const auth = useAuthStore();

const name = ref("");
const shortName = ref("");
const description = ref("");
const joinMode = ref<"open" | "request" | "invite_only">("open");
const saving = ref(false);
const error = ref("");
const canOwnClub = ref(false);
const checkingTag = ref(true);

const isAdmin = computed(() => !!auth.isAdmin);

async function loadOwnClub() {
  checkingTag.value = true;
  canOwnClub.value = isAdmin.value;
  if (!auth.me) {
    checkingTag.value = false;
    return;
  }
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
      variables: { steam_id: String(auth.me.steam_id), tag: OWN_CLUB_TAG },
      fetchPolicy: "network-only",
    });
    canOwnClub.value = isAdmin.value || (data?.player_tags?.length ?? 0) > 0;
  } catch {
    canOwnClub.value = isAdmin.value;
  } finally {
    checkingTag.value = false;
  }
}

onMounted(() => {
  void loadOwnClub();
});

async function createClub() {
  if (!auth.me) {
    await navigateTo("/login");
    return;
  }
  if (!canOwnClub.value) {
    error.value = t("pages.clubs.ownclub_required");
    return;
  }
  const n = name.value.trim();
  if (n.length < 2) {
    error.value = t("pages.clubs.form.name_required");
    return;
  }
  saving.value = true;
  error.value = "";
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation CreateClub(
          $name: String!
          $short_name: String
          $description: String!
          $join_mode: String!
        ) {
          createClub(
            name: $name
            short_name: $short_name
            description: $description
            join_mode: $join_mode
          ) {
            success
            id
            slug
          }
        }
      `,
      variables: {
        name: n,
        short_name: shortName.value.trim() || null,
        description: description.value.trim(),
        join_mode: joinMode.value,
      },
    });
    const club = data?.createClub;
    if (!club?.id) throw new Error("create failed");
    await router.push(`/clubs/${club.slug || club.id}`);
  } catch (e: any) {
    error.value = clubErrorMessage(e, t) || t("pages.clubs.form.create_failed");
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <PageTransition class="mx-auto w-full max-w-2xl">
    <TacticalPageHeader>
      <template #title>{{ t("pages.clubs.create") }}</template>
    </TacticalPageHeader>
  </PageTransition>
  <PageTransition :delay="80" class="mx-auto mt-6 w-full max-w-2xl">
    <div class="overflow-hidden rounded-xl border border-border/70 bg-card/40">
      <div
        class="h-28 bg-[radial-gradient(ellipse_at_top_left,hsl(var(--tac-amber)/0.32),transparent_52%),linear-gradient(180deg,#1c1c20_0%,#0b0b0d_100%)]"
      />
      <form class="grid gap-4 p-5 sm:p-6" @submit.prevent="createClub">
        <label class="grid gap-1.5 text-sm">
          <span class="text-xs font-medium text-muted-foreground">
            {{ t("pages.clubs.form.name") }}
          </span>
          <Input v-model="name" maxlength="48" required />
        </label>
        <label class="grid gap-1.5 text-sm">
          <span class="text-xs font-medium text-muted-foreground">
            {{ t("pages.clubs.form.short_name") }}
          </span>
          <Input v-model="shortName" maxlength="5" class="font-mono uppercase" />
        </label>
        <label class="grid gap-1.5 text-sm">
          <span class="text-xs font-medium text-muted-foreground">
            {{ t("pages.clubs.form.description") }}
          </span>
          <textarea
            v-model="description"
            rows="4"
            class="rounded-lg border border-border bg-background/40 px-3 py-2 text-sm outline-none focus:border-[hsl(var(--tac-amber)/0.5)]"
          />
        </label>
        <label class="grid gap-1.5 text-sm">
          <span class="text-xs font-medium text-muted-foreground">
            {{ t("pages.clubs.form.join_mode") }}
          </span>
          <select
            v-model="joinMode"
            class="h-9 rounded-lg border border-border bg-background/40 px-3 text-sm"
          >
            <option value="open">{{ t("pages.clubs.join_modes.open") }}</option>
            <option value="request">{{ t("pages.clubs.join_modes.request") }}</option>
            <option value="invite_only">
              {{ t("pages.clubs.join_modes.invite_only") }}
            </option>
          </select>
        </label>
        <p v-if="!checkingTag && !canOwnClub" class="text-sm text-destructive">
          {{ t("pages.clubs.ownclub_required") }}
        </p>
        <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
        <Button
          type="submit"
          :disabled="saving || checkingTag || !canOwnClub"
          :class="tacticalCtaButtonClasses"
        >
          {{ saving ? t("common.saving") : t("pages.clubs.form.submit") }}
        </Button>
      </form>
    </div>
  </PageTransition>
</template>
