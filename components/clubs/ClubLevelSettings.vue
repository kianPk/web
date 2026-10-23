<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import {
  CLUB_PERM_KEYS,
  DEFAULT_LEVEL_PERMISSIONS,
  type ClubPermKey,
} from "~/utilities/clubLevels";
import { clubErrorMessage } from "~/utilities/clubErrors";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

const props = defineProps<{
  clubId: string;
  levelPermissions: Record<string, { min?: number }> | null;
  members: Array<{
    steam_id: string | number;
    level?: number;
    title?: string | null;
    player?: { name?: string | null } | null;
  }>;
}>();

const emit = defineEmits<{
  saved: [perms: Record<string, { min: number }>];
}>();

const { t } = useI18n();
const { client } = useApolloClient();
const busy = ref(false);
const error = ref("");
const mins = ref<Record<string, number>>({});
const search = ref("");
const overridePerm = ref<ClubPermKey>("post_news");
const overrideAllowed = ref<"true" | "false" | "clear">("true");

watch(
  () => props.levelPermissions,
  (value) => {
    const next: Record<string, number> = {};
    for (const key of CLUB_PERM_KEYS) {
      next[key] = Number(
        value?.[key]?.min ?? DEFAULT_LEVEL_PERMISSIONS[key].min,
      );
    }
    mins.value = next;
  },
  { immediate: true, deep: true },
);

const matchedMembers = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) return [];
  return (props.members || [])
    .filter((m) => {
      const name = String(m.player?.name || "").toLowerCase();
      const sid = String(m.steam_id);
      return name.includes(q) || sid.includes(q);
    })
    .slice(0, 8);
});

async function saveDefaults() {
  busy.value = true;
  error.value = "";
  try {
    const permissions: Record<string, { min: number }> = {};
    for (const key of CLUB_PERM_KEYS) {
      permissions[key] = {
        min: Math.min(10, Math.max(1, Math.floor(Number(mins.value[key]) || 1))),
      };
    }
    const { data } = await client.mutate({
      mutation: gql`
        mutation ($club_id: uuid!, $permissions: jsonb!) {
          saveClubLevelPermissions(club_id: $club_id, permissions: $permissions) {
            success
          }
        }
      `,
      variables: { club_id: props.clubId, permissions },
    });
    if (!data?.saveClubLevelPermissions?.success) {
      error.value = t("pages.clubs.action_failed");
      return;
    }
    emit("saved", permissions);
  } catch (e: any) {
    error.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

async function applyOverride(steamId: string) {
  busy.value = true;
  error.value = "";
  try {
    const allowed =
      overrideAllowed.value === "clear"
        ? null
        : overrideAllowed.value === "true";
    const { data } = await client.mutate({
      mutation: gql`
        mutation (
          $club_id: uuid!
          $steam_id: String!
          $perm_key: String!
          $allowed: Boolean
        ) {
          setClubMemberPermOverride(
            club_id: $club_id
            steam_id: $steam_id
            perm_key: $perm_key
            allowed: $allowed
          ) {
            success
          }
        }
      `,
      variables: {
        club_id: props.clubId,
        steam_id: String(steamId),
        perm_key: overridePerm.value,
        allowed,
      },
    });
    if (!data?.setClubMemberPermOverride?.success) {
      error.value = t("pages.clubs.action_failed");
    }
  } catch (e: any) {
    error.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div class="space-y-4 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2">
    <h3 class="text-sm font-semibold">{{ t("pages.clubs.levels_settings") }}</h3>
    <p class="text-xs text-muted-foreground">{{ t("pages.clubs.levels_settings_hint") }}</p>

    <div class="grid gap-2 sm:grid-cols-2">
      <label
        v-for="key in CLUB_PERM_KEYS"
        :key="key"
        class="flex items-center justify-between gap-2 rounded-lg border border-border/50 px-3 py-2 text-xs"
      >
        <span>{{ t(`pages.clubs.perms.${key}`) }}</span>
        <Input
          v-model.number="mins[key]"
          type="number"
          min="1"
          max="10"
          class="h-8 w-16"
        />
      </label>
    </div>
    <Button type="button" :disabled="busy" :class="tacticalCtaButtonClasses" @click="saveDefaults">
      {{ t("pages.clubs.save_level_perms") }}
    </Button>

    <div class="border-t border-border/60 pt-4">
      <h4 class="text-sm font-semibold">{{ t("pages.clubs.member_override") }}</h4>
      <p class="mt-1 text-xs text-muted-foreground">
        {{ t("pages.clubs.member_override_hint") }}
      </p>
      <Input
        v-model="search"
        class="mt-2"
        :placeholder="t('pages.clubs.member_override_search')"
      />
      <div class="mt-2 flex flex-wrap gap-2">
        <select
          v-model="overridePerm"
          class="h-9 rounded-md border border-border bg-background px-2 text-sm"
        >
          <option v-for="key in CLUB_PERM_KEYS" :key="key" :value="key">
            {{ t(`pages.clubs.perms.${key}`) }}
          </option>
        </select>
        <select
          v-model="overrideAllowed"
          class="h-9 rounded-md border border-border bg-background px-2 text-sm"
        >
          <option value="true">{{ t("pages.clubs.override_allow") }}</option>
          <option value="false">{{ t("pages.clubs.override_deny") }}</option>
          <option value="clear">{{ t("pages.clubs.override_clear") }}</option>
        </select>
      </div>
      <ul class="mt-2 space-y-1">
        <li
          v-for="m in matchedMembers"
          :key="String(m.steam_id)"
          class="flex flex-wrap items-center gap-2 text-sm"
        >
          <span class="min-w-0 flex-1 truncate">
            {{ m.player?.name || m.steam_id }}
            <span class="text-xs text-muted-foreground">· L{{ m.level || 1 }}</span>
          </span>
          <Button size="sm" variant="outline" :disabled="busy" @click="applyOverride(String(m.steam_id))">
            {{ t("pages.clubs.apply_override") }}
          </Button>
        </li>
      </ul>
    </div>
    <p v-if="error" class="text-xs text-destructive">{{ error }}</p>
  </div>
</template>
