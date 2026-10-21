<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

const route = useRoute();
const { t } = useI18n();
const { client } = useApolloClient();
const auth = useAuthStore();
const code = computed(() => String(route.params.code || ""));
const busy = ref(false);
const error = ref("");
const done = ref(false);

async function redeem() {
  if (!auth.me) {
    await navigateTo("/login");
    return;
  }
  busy.value = true;
  error.value = "";
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation($code: String!) {
          redeemClubInviteLink(code: $code) {
            success
            club_id
          }
        }
      `,
      variables: { code: code.value },
    });
    const clubId = data?.redeemClubInviteLink?.club_id;
    done.value = true;
    if (clubId) await navigateTo(`/clubs/${clubId}`);
  } catch (e: any) {
    error.value = e?.message || t("pages.clubs.invite.link_failed");
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <PageTransition class="mx-auto max-w-lg">
    <TacticalPageHeader>
      <template #title>{{ t("pages.clubs.invite.redeem_title") }}</template>
    </TacticalPageHeader>
    <div class="mt-6 overflow-hidden rounded-xl border border-border/70 bg-card/40 p-6">
      <p class="text-sm text-muted-foreground">
        {{ t("pages.clubs.invite.redeem_desc") }}
      </p>
      <p v-if="error" class="mt-3 text-sm text-destructive">{{ error }}</p>
      <Button
        class="mt-6"
        :class="tacticalCtaButtonClasses"
        :disabled="busy || done"
        @click="redeem"
      >
        {{ busy ? t("common.loading") : t("pages.clubs.invite.redeem") }}
      </Button>
    </div>
  </PageTransition>
</template>
