<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import gql from "graphql-tag";
import { Ban, ShieldOff, RefreshCw, Search } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import PlayerSearch from "~/components/PlayerSearch.vue";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import SanctionPlayer from "~/components/SanctionPlayer.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Badge } from "~/components/ui/badge";
import { Spinner } from "~/components/ui/spinner";
import { toast } from "~/components/ui/toast";
import TimeAgo from "~/components/TimeAgo.vue";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "~/components/ui/alert-dialog";

definePageMeta({
  middleware: "admin",
});

const { t } = useI18n();
const { client: apollo } = useApolloClient();

type BanRow = {
  steam_id: string;
  name: string | null;
  avatar_url: string | null;
  is_banned: boolean;
  banned_until: string | null;
  reason: string | null;
  created_at: string | null;
  remove_sanction_date: string | null;
};

const loading = ref(false);
const rows = ref<BanRow[]>([]);
const filter = ref("");
const selected = ref<any>(null);
const unbanTarget = ref<BanRow | null>(null);
const unbanning = ref(false);

const filtered = computed(() => {
  const q = filter.value.trim().toLowerCase();
  if (!q) return rows.value;
  return rows.value.filter((r) => {
    const name = (r.name || "").toLowerCase();
    const steam = String(r.steam_id);
    const reason = (r.reason || "").toLowerCase();
    return name.includes(q) || steam.includes(q) || reason.includes(q);
  });
});

function isAcBan(reason: string | null | undefined) {
  return !!reason && reason.startsWith("YGuard AC:");
}

function expiryLabel(row: BanRow) {
  if (!row.remove_sanction_date) {
    return t("pages.settings.application.bans.permanent");
  }
  const until = new Date(row.remove_sanction_date);
  if (until <= new Date()) {
    return t("pages.settings.application.bans.expired");
  }
  return until.toLocaleString();
}

async function loadBans() {
  loading.value = true;
  try {
    // Query sanctions directly (guest/admin inherit filter deleted_at IS NULL).
    // Avoid filtering players by computed is_banned — that fails on some roles.
    const { data } = await apollo.query({
      query: gql`
        query AdminBannedPlayers {
          player_sanctions(
            where: { type: { _eq: ban } }
            order_by: { created_at: desc }
            limit: 1000
          ) {
            reason
            created_at
            remove_sanction_date
            player_steam_id
            player {
              steam_id
              name
              avatar_url
              is_banned
              banned_until
            }
          }
        }
      `,
      fetchPolicy: "network-only",
      errorPolicy: "none",
    });

    const now = Date.now();
    const bySteam = new Map<string, BanRow>();
    for (const s of data?.player_sanctions || []) {
      const steam = String(s.player_steam_id || s.player?.steam_id || "");
      if (!steam) continue;
      const until = s.remove_sanction_date
        ? new Date(s.remove_sanction_date).getTime()
        : null;
      if (until !== null && until <= now) continue;
      if (bySteam.has(steam)) continue;
      bySteam.set(steam, {
        steam_id: steam,
        name: s.player?.name ?? null,
        avatar_url: s.player?.avatar_url ?? null,
        is_banned: s.player?.is_banned ?? true,
        banned_until: s.player?.banned_until ?? s.remove_sanction_date ?? null,
        reason: s.reason ?? null,
        created_at: s.created_at ?? null,
        remove_sanction_date: s.remove_sanction_date ?? null,
      });
    }
    rows.value = [...bySteam.values()].sort((a, b) =>
      (a.name || a.steam_id).localeCompare(b.name || b.steam_id),
    );
  } catch (err: any) {
    console.error(err);
    const detail =
      err?.graphQLErrors?.[0]?.message ||
      err?.networkError?.result?.errors?.[0]?.message ||
      err?.message;
    toast({
      title: t("pages.settings.application.bans.load_failed"),
      description: detail ? String(detail).slice(0, 180) : undefined,
      variant: "destructive",
    });
  } finally {
    loading.value = false;
  }
}

async function confirmUnban() {
  if (!unbanTarget.value || unbanning.value) return;
  unbanning.value = true;
  try {
    await apollo.mutate({
      mutation: gql`
        mutation AdminUnbanPlayer($steam_id: String!, $type: String!) {
          unsanctionServerPlayer(steam_id: $steam_id, type: $type) {
            enforced
            message
          }
        }
      `,
      variables: {
        steam_id: unbanTarget.value.steam_id,
        type: "ban",
      },
    });
    toast({
      title: t("pages.settings.application.bans.unbanned", {
        name: unbanTarget.value.name || unbanTarget.value.steam_id,
      }),
    });
    unbanTarget.value = null;
    await loadBans();
  } catch (err) {
    console.error(err);
    toast({
      title: t("pages.settings.application.bans.unban_failed"),
      variant: "destructive",
    });
  } finally {
    unbanning.value = false;
  }
}

function onSelected(player: any) {
  selected.value = player;
}

function onSanctioned() {
  selected.value = null;
  void loadBans();
}

onMounted(() => {
  void loadBans();
});
</script>

<template>
  <SettingsPage width="full">
    <PageTransition :delay="0">
      <SettingsSection
        id="ban-player"
        :title="$t('pages.settings.application.bans.ban_section')"
        :description="$t('pages.settings.application.bans.ban_section_desc')"
      >
        <div class="space-y-4">
          <PlayerSearch
            :label="$t('pages.settings.application.bans.search_player')"
            :selected="selected"
            @selected="onSelected"
          />
          <div
            v-if="selected"
            class="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border bg-card/40 p-3"
          >
            <PlayerDisplay :player="selected" />
            <div class="flex items-center gap-2">
              <SanctionPlayer
                :player="selected"
                variant="block"
                @sanctioned="onSanctioned"
              />
              <Button
                v-if="selected.is_banned"
                variant="outline"
                class="gap-2"
                @click="
                  unbanTarget = {
                    steam_id: String(selected.steam_id),
                    name: selected.name,
                    avatar_url: selected.avatar_url,
                    is_banned: true,
                    banned_until: selected.banned_until ?? null,
                    reason: null,
                    created_at: null,
                    remove_sanction_date: null,
                  }
                "
              >
                <ShieldOff class="h-4 w-4" />
                {{ $t("pages.settings.application.bans.unban") }}
              </Button>
            </div>
          </div>
        </div>
      </SettingsSection>
    </PageTransition>

    <PageTransition :delay="60">
      <SettingsSection
        id="banned-list"
        :title="$t('pages.settings.application.bans.list_section')"
        :description="$t('pages.settings.application.bans.list_section_desc')"
      >
        <template #action>
          <Button
            variant="outline"
            size="sm"
            class="gap-2"
            :disabled="loading"
            @click="loadBans"
          >
            <RefreshCw class="h-3.5 w-3.5" :class="{ 'animate-spin': loading }" />
            {{ $t("common.refresh") }}
          </Button>
        </template>

        <div class="mb-4 flex items-center gap-2">
          <div class="relative flex-1 max-w-md">
            <Search
              class="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground"
            />
            <Input
              v-model="filter"
              class="pl-8"
              :placeholder="$t('pages.settings.application.bans.filter_placeholder')"
            />
          </div>
          <Badge variant="secondary" class="tabular-nums">
            {{ filtered.length }}
          </Badge>
        </div>

        <div v-if="loading && !rows.length" class="flex justify-center py-10">
          <Spinner class="h-6 w-6" />
        </div>

        <div
          v-else-if="!filtered.length"
          class="rounded-md border border-dashed border-border px-4 py-10 text-center text-sm text-muted-foreground"
        >
          {{ $t("pages.settings.application.bans.empty") }}
        </div>

        <div v-else class="divide-y divide-border rounded-md border border-border">
          <div
            v-for="row in filtered"
            :key="row.steam_id"
            class="flex flex-col gap-3 p-3 sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="min-w-0 flex-1 space-y-1">
              <div class="flex flex-wrap items-center gap-2">
                <PlayerDisplay
                  :player="{
                    steam_id: row.steam_id,
                    name: row.name,
                    avatar_url: row.avatar_url,
                    is_banned: true,
                  }"
                />
                <Badge v-if="isAcBan(row.reason)" variant="destructive">
                  AC
                </Badge>
                <Badge variant="outline" class="font-mono text-[0.65rem]">
                  {{ expiryLabel(row) }}
                </Badge>
              </div>
              <p class="truncate text-xs text-muted-foreground">
                <span class="font-mono">{{ row.steam_id }}</span>
                <template v-if="row.reason"> · {{ row.reason }}</template>
              </p>
              <p v-if="row.created_at" class="text-[0.7rem] text-muted-foreground/80">
                <TimeAgo :date="row.created_at" />
              </p>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              <NuxtLink
                :to="`/players/${row.steam_id}`"
                class="text-xs text-muted-foreground underline-offset-2 hover:underline"
              >
                {{ $t("pages.settings.application.bans.profile") }}
              </NuxtLink>
              <Button
                variant="outline"
                size="sm"
                class="gap-1.5"
                @click="unbanTarget = row"
              >
                <ShieldOff class="h-3.5 w-3.5" />
                {{ $t("pages.settings.application.bans.unban") }}
              </Button>
            </div>
          </div>
        </div>
      </SettingsSection>
    </PageTransition>
  </SettingsPage>

  <AlertDialog
    :open="!!unbanTarget"
    @update:open="(o) => !o && (unbanTarget = null)"
  >
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>
          {{ $t("pages.settings.application.bans.unban_confirm_title") }}
        </AlertDialogTitle>
        <AlertDialogDescription>
          {{
            $t("pages.settings.application.bans.unban_confirm_desc", {
              name: unbanTarget?.name || unbanTarget?.steam_id,
            })
          }}
        </AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogFooter>
        <AlertDialogCancel :disabled="unbanning">
          {{ $t("common.cancel") }}
        </AlertDialogCancel>
        <AlertDialogAction :disabled="unbanning" @click.prevent="confirmUnban">
          <Ban class="mr-1.5 h-3.5 w-3.5" />
          {{ $t("pages.settings.application.bans.unban") }}
        </AlertDialogAction>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
