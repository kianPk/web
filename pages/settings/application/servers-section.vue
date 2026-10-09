<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import gql from "graphql-tag";
import {
  useApolloClient,
  useQuery,
  useSubscription,
} from "@vue/apollo-composable";
import { ExternalLink, Loader2 } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { toast } from "@/components/ui/toast";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import { SERVER_MODES, type ServerModeKey } from "~/utilities/serverModes";

definePageMeta({
  middleware: "admin",
});

// Raw documents: servers.section_mode is newer than the generated client.
const SETTINGS_QUERY = gql`
  query ServersSectionSettings {
    settings(where: { name: { _like: "servers_section_%" } }) {
      name
      value
    }
  }
`;

const SERVERS_SUBSCRIPTION = gql`
  subscription ServersSectionServers {
    servers(
      where: { section_mode: { _is_null: false } }
      order_by: { label: asc }
    ) {
      id
      label
      section_mode
      enabled
      connected
      boot_status
      game_server_node_id
    }
  }
`;

const SAVE_SETTINGS = gql`
  mutation SaveServersSectionSettings($objects: [settings_insert_input!]!) {
    insert_settings(
      objects: $objects
      on_conflict: { constraint: settings_pkey, update_columns: [value] }
    ) {
      affected_rows
    }
  }
`;

const RESERVE_SETTING = "servers_section_reserve_slots";
const DEFAULT_RESERVE = 2;
const MAX_PER_MODE = 50;

const { t } = useI18n();
const { client } = useApolloClient();

const { result: settingsResult, refetch } = useQuery(SETTINGS_QUERY, null, {
  fetchPolicy: "network-only",
});
const { result: serversResult } = useSubscription(SERVERS_SUBSCRIPTION);

const counts = ref<Record<ServerModeKey, number>>({
  duels: 0,
  awp: 0,
  "2x2": 0,
});
const reserve = ref(DEFAULT_RESERVE);
const saving = ref(false);

watch(
  settingsResult,
  (value) => {
    const rows = (value?.settings || []) as Array<{
      name: string;
      value: string;
    }>;
    const read = (name: string) =>
      rows.find((row) => row.name === name)?.value;
    for (const mode of SERVER_MODES) {
      counts.value[mode.key] =
        parseInt(read(`servers_section_${mode.key}`) ?? "", 10) || 0;
    }
    const savedReserve = parseInt(read(RESERVE_SETTING) ?? "", 10);
    reserve.value = Number.isFinite(savedReserve)
      ? savedReserve
      : DEFAULT_RESERVE;
  },
  { immediate: true },
);

type SectionServer = {
  id: string;
  label: string;
  section_mode: ServerModeKey;
  enabled: boolean;
  connected: boolean;
  boot_status: string | null;
  game_server_node_id: string | null;
};

const serversByMode = computed(() => {
  const rows = (serversResult.value?.servers || []) as SectionServer[];
  const number = (label: string) =>
    Number(/#(\d+)\s*$/.exec(label)?.[1] ?? Number.MAX_SAFE_INTEGER);
  return Object.fromEntries(
    SERVER_MODES.map((mode) => [
      mode.key,
      rows
        .filter((row) => row.section_mode === mode.key)
        .sort((a, b) => number(a.label) - number(b.label)),
    ]),
  ) as Record<ServerModeKey, SectionServer[]>;
});

function clamp(value: number, max: number) {
  return Math.min(Math.max(Math.floor(Number(value) || 0), 0), max);
}

async function save() {
  saving.value = true;
  try {
    await client.mutate({
      mutation: SAVE_SETTINGS,
      variables: {
        objects: [
          ...SERVER_MODES.map((mode) => ({
            name: `servers_section_${mode.key}`,
            value: String(clamp(counts.value[mode.key], MAX_PER_MODE)),
          })),
          { name: RESERVE_SETTING, value: String(clamp(reserve.value, 64)) },
        ],
      },
    });
    await refetch();
    toast({ title: t("pages.settings.application.servers_section.saved") });
  } catch (error: any) {
    toast({
      title: error?.message ?? String(error),
      variant: "destructive",
    });
  } finally {
    saving.value = false;
  }
}

function statusOf(server: SectionServer) {
  if (!server.enabled) return "disabled";
  if (server.connected) return "online";
  return "booting";
}
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <div class="space-y-6">
        <SettingsSection
          id="servers-section-counts"
          :title="$t('pages.settings.application.servers_section.title')"
          :description="
            $t('pages.settings.application.servers_section.description')
          "
        >
          <div class="grid gap-3 sm:grid-cols-3">
            <label
              v-for="mode in SERVER_MODES"
              :key="mode.key"
              class="space-y-1.5 rounded-lg border border-border/60 bg-muted/20 p-4"
            >
              <span class="text-sm font-semibold">{{
                $t(`pages.servers.modes.${mode.key}.name`)
              }}</span>
              <Input
                v-model="counts[mode.key]"
                type="number"
                min="0"
                :max="MAX_PER_MODE"
                class="h-9"
              />
              <span class="block text-xs text-muted-foreground">{{
                $t("pages.settings.application.servers_section.count_hint")
              }}</span>
            </label>
          </div>

          <label
            class="flex flex-col gap-1.5 rounded-lg border border-border/60 bg-muted/20 p-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <span>
              <span class="block text-sm font-semibold">{{
                $t("pages.settings.application.servers_section.reserve")
              }}</span>
              <span class="block text-xs text-muted-foreground">{{
                $t("pages.settings.application.servers_section.reserve_hint")
              }}</span>
            </span>
            <Input
              v-model="reserve"
              type="number"
              min="0"
              max="64"
              class="h-9 w-28"
            />
          </label>

          <div class="flex justify-end">
            <Button :disabled="saving" @click="save">
              <Loader2 v-if="saving" class="h-4 w-4 animate-spin" />
              {{ $t("pages.settings.application.servers_section.save") }}
            </Button>
          </div>
        </SettingsSection>

        <SettingsSection
          id="servers-section-fleet"
          :title="$t('pages.settings.application.servers_section.fleet')"
          :description="
            $t('pages.settings.application.servers_section.fleet_hint')
          "
        >
          <div class="grid gap-3 sm:grid-cols-3">
            <div
              v-for="mode in SERVER_MODES"
              :key="mode.key"
              class="rounded-lg border border-border/60 p-3"
            >
              <p class="mb-2 text-sm font-semibold">
                {{ $t(`pages.servers.modes.${mode.key}.name`) }}
                <span class="font-mono text-xs text-muted-foreground"
                  >{{ serversByMode[mode.key].length }}/{{
                    counts[mode.key]
                  }}</span
                >
              </p>
              <p
                v-if="serversByMode[mode.key].length === 0"
                class="text-xs text-muted-foreground"
              >
                {{ $t("pages.settings.application.servers_section.none") }}
              </p>
              <ul class="space-y-1">
                <li
                  v-for="server in serversByMode[mode.key]"
                  :key="server.id"
                >
                  <NuxtLink
                    :to="`/dedicated-servers/${server.id}`"
                    class="flex items-center gap-2 rounded px-2 py-1 text-xs hover:bg-muted/50"
                  >
                    <span
                      class="h-2 w-2 shrink-0 rounded-full"
                      :class="{
                        'bg-emerald-500': statusOf(server) === 'online',
                        'bg-amber-500': statusOf(server) === 'booting',
                        'bg-muted-foreground/40':
                          statusOf(server) === 'disabled',
                      }"
                    />
                    <span class="flex-1 truncate">{{ server.label }}</span>
                    <span class="text-muted-foreground">{{
                      $t(
                        `pages.settings.application.servers_section.status.${statusOf(server)}`,
                      )
                    }}</span>
                    <ExternalLink class="h-3 w-3 text-muted-foreground" />
                  </NuxtLink>
                </li>
              </ul>
            </div>
          </div>
        </SettingsSection>
      </div>
    </PageTransition>
  </SettingsPage>
</template>
