<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

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

async function createClub() {
  if (!auth.me) {
    await navigateTo("/login");
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
          $join_mode: e_club_join_modes_enum!
        ) {
          insert_clubs_one(
            object: {
              name: $name
              short_name: $short_name
              description: $description
              join_mode: $join_mode
            }
          ) {
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
    const club = data?.insert_clubs_one;
    if (!club?.id) throw new Error("create failed");
    await router.push(`/clubs/${club.slug || club.id}`);
  } catch (e: any) {
    error.value = e?.message || t("pages.clubs.form.create_failed");
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
    <form class="grid gap-4" @submit.prevent="createClub">
      <label class="grid gap-1.5 text-sm">
        <span class="font-mono text-[0.65rem] uppercase tracking-[0.18em] text-muted-foreground">
          {{ t("pages.clubs.form.name") }}
        </span>
        <Input v-model="name" maxlength="48" required />
      </label>
      <label class="grid gap-1.5 text-sm">
        <span class="font-mono text-[0.65rem] uppercase tracking-[0.18em] text-muted-foreground">
          {{ t("pages.clubs.form.short_name") }}
        </span>
        <Input v-model="shortName" maxlength="5" class="font-mono uppercase" />
      </label>
      <label class="grid gap-1.5 text-sm">
        <span class="font-mono text-[0.65rem] uppercase tracking-[0.18em] text-muted-foreground">
          {{ t("pages.clubs.form.description") }}
        </span>
        <textarea
          v-model="description"
          rows="4"
          class="rounded-md border border-border bg-card/60 px-3 py-2 text-sm outline-none focus:border-[hsl(var(--tac-amber)/0.5)]"
        />
      </label>
      <label class="grid gap-1.5 text-sm">
        <span class="font-mono text-[0.65rem] uppercase tracking-[0.18em] text-muted-foreground">
          {{ t("pages.clubs.form.join_mode") }}
        </span>
        <select
          v-model="joinMode"
          class="h-9 rounded-md border border-border bg-card/60 px-3 text-sm"
        >
          <option value="open">{{ t("pages.clubs.join_modes.open") }}</option>
          <option value="request">{{ t("pages.clubs.join_modes.request") }}</option>
          <option value="invite_only">
            {{ t("pages.clubs.join_modes.invite_only") }}
          </option>
        </select>
      </label>
      <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
      <Button type="submit" :disabled="saving" :class="tacticalCtaButtonClasses">
        {{ saving ? t("common.saving") : t("pages.clubs.form.submit") }}
      </Button>
    </form>
  </PageTransition>
</template>
