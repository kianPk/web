<script setup lang="ts">
import { Loader2, Server } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { SERVER_MODES, type ServerModeKey } from "~/utilities/serverModes";

const props = defineProps<{
  clubId: string;
  canManage: boolean;
}>();

type ClubServer = {
  id: string;
  label: string | null;
  section_mode: string | null;
  connected: boolean;
  enabled: boolean;
  link: string | null;
};

type AvailableServer = {
  id: string;
  label: string | null;
  section_mode: string | null;
};

const { t } = useI18n();
const servers = ref<ClubServer[]>([]);
const available = ref<AvailableServer[]>([]);
const manage = ref(false);
const loading = ref(true);
const busy = ref(false);
const error = ref("");
const attachId = ref("");
const mapChoice = ref<Record<string, string>>({});
const changingMap = ref("");
const pools = ref<Record<string, Array<{ id: string; name: string }>>>({});

const grouped = computed(() => {
  const known = new Set(SERVER_MODES.map((mode) => mode.key));
  return SERVER_MODES.map((mode) => ({
    key: mode.key,
    servers: servers.value.filter((server) => server.section_mode === mode.key),
  })).concat(
    servers.value.some((server) => !known.has(server.section_mode as ServerModeKey))
      ? [
          {
            key: "other" as const,
            servers: servers.value.filter(
              (server) => !known.has(server.section_mode as ServerModeKey),
            ),
          },
        ]
      : [],
  );
});

function serverLabel(server: AvailableServer) {
  const name = server.label || server.id;
  return server.section_mode ? `${name} · ${server.section_mode}` : name;
}

function statusOf(server: ClubServer) {
  if (!server.enabled) return "disabled";
  if (server.connected) return "online";
  return "booting";
}

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const data = await $fetch<{
      servers: ClubServer[];
      manage: boolean;
      available: AvailableServer[];
    }>(`https://${apiDomain}/clubs/${props.clubId}/servers`, {
      credentials: "include",
    });
    servers.value = data.servers ?? [];
    available.value = data.available ?? [];
    manage.value = !!data.manage && props.canManage;
    if (manage.value) void loadPools();
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || String(err);
  } finally {
    loading.value = false;
  }
}

async function loadPools() {
  const apiDomain = useRuntimeConfig().public.apiDomain;
  await Promise.all(
    SERVER_MODES.map(async (mode) => {
      try {
        const data = await $fetch<{ maps: Array<{ id: string; name: string }> }>(
          `https://${apiDomain}/hosted-servers/section-maps/${mode.key}`,
        );
        pools.value = { ...pools.value, [mode.key]: data.maps ?? [] };
      } catch {
        pools.value = { ...pools.value, [mode.key]: [] };
      }
    }),
  );
}

async function attach() {
  if (!attachId.value) return;
  busy.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    await $fetch(`https://${apiDomain}/clubs/${props.clubId}/servers`, {
      method: "POST",
      credentials: "include",
      body: { server_id: attachId.value },
    });
    attachId.value = "";
    await load();
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || String(err);
  } finally {
    busy.value = false;
  }
}

async function detach(serverId: string) {
  busy.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    await $fetch(
      `https://${apiDomain}/clubs/${props.clubId}/servers/${serverId}`,
      { method: "DELETE", credentials: "include" },
    );
    await load();
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || String(err);
  } finally {
    busy.value = false;
  }
}

async function changeMap(server: ClubServer) {
  const map = mapChoice.value[server.id];
  if (!map) return;
  changingMap.value = server.id;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    await $fetch(
      `https://${apiDomain}/clubs/${props.clubId}/servers/${server.id}/map`,
      {
        method: "POST",
        credentials: "include",
        body: { map },
      },
    );
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || String(err);
  } finally {
    changingMap.value = "";
  }
}

watch(() => props.clubId, () => void load(), { immediate: true });
</script>

<template>
  <section class="space-y-4">
    <p class="text-sm text-muted-foreground">
      {{ t("pages.clubs.servers_hint") }}
    </p>

    <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
    <p v-if="loading" class="text-sm text-muted-foreground">...</p>

    <div
      v-else-if="manage"
      class="flex flex-wrap items-center gap-2 rounded-xl border border-border/70 bg-card/40 p-3"
    >
      <Server class="h-4 w-4 text-muted-foreground" />
      <select
        v-model="attachId"
        class="h-9 min-w-0 flex-1 rounded-md border border-border bg-card px-2 text-sm"
      >
        <option value="">{{ t("pages.clubs.servers_pick") }}</option>
        <option v-for="server in available" :key="server.id" :value="server.id">
          {{ serverLabel(server) }}
        </option>
      </select>
      <Button :disabled="busy || !attachId" @click="attach">
        {{ t("pages.clubs.servers_attach") }}
      </Button>
      <p v-if="!available.length" class="w-full text-xs text-muted-foreground">
        {{ t("pages.clubs.servers_none_available") }}
      </p>
    </div>

    <p
      v-if="!loading && !servers.length && !manage"
      class="rounded-xl border border-border/70 bg-card/40 p-8 text-center text-sm text-muted-foreground"
    >
      {{ t("pages.clubs.servers_empty") }}
    </p>

    <div v-if="!loading && (servers.length || manage)" class="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      <div
        v-for="group in grouped.filter((group) => group.servers.length || manage)"
        :key="group.key"
        class="rounded-xl border border-border/70 bg-card/40 p-3"
      >
        <p class="mb-2 text-sm font-semibold">
          {{
            group.key === "other"
              ? t("pages.clubs.servers_other")
              : t(`pages.servers.modes.${group.key}.name`)
          }}
          <span class="font-mono text-xs text-muted-foreground">
            {{ group.servers.length }}
          </span>
        </p>
        <p v-if="!group.servers.length" class="text-xs text-muted-foreground">
          {{ t("pages.clubs.servers_empty") }}
        </p>
        <ul class="space-y-2">
          <li v-for="server in group.servers" :key="server.id">
            <div class="flex items-center gap-2 text-xs">
              <span
                class="h-2 w-2 shrink-0 rounded-full"
                :class="{
                  'bg-emerald-500': statusOf(server) === 'online',
                  'bg-amber-500': statusOf(server) === 'booting',
                  'bg-muted-foreground/40': statusOf(server) === 'disabled',
                }"
              />
              <span class="flex-1 truncate">{{ server.label || server.id }}</span>
              <span class="text-muted-foreground">
                {{
                  t(
                    `pages.settings.application.servers_section.status.${statusOf(server)}`,
                  )
                }}
              </span>
            </div>
            <div class="mt-1 flex flex-wrap items-center gap-1">
              <a
                v-if="server.link"
                :href="server.link"
                class="rounded-md border border-border px-2 py-1 text-xs"
              >
                {{ t("pages.clubs.servers_connect") }}
              </a>
              <Button
                v-if="manage"
                size="sm"
                variant="ghost"
                class="h-7 px-2 text-xs text-destructive"
                :disabled="busy"
                @click="detach(server.id)"
              >
                {{ t("pages.clubs.servers_detach") }}
              </Button>
            </div>
            <div
              v-if="manage && server.enabled && pools[server.section_mode || '']"
              class="mt-1 flex items-center gap-1"
            >
              <select
                v-model="mapChoice[server.id]"
                class="h-7 min-w-0 flex-1 rounded-md border border-border bg-card px-2 text-xs"
                dir="ltr"
              >
                <option :value="undefined" disabled>
                  {{
                    t(
                      "pages.settings.application.servers_section.change_map_pick",
                    )
                  }}
                </option>
                <option
                  v-for="map in pools[server.section_mode || '']"
                  :key="map.id"
                  :value="map.id"
                >
                  {{ map.name || map.id }}
                </option>
              </select>
              <Button
                size="sm"
                variant="outline"
                class="h-7 shrink-0 px-2 text-xs"
                :disabled="!mapChoice[server.id] || changingMap === server.id"
                @click="changeMap(server)"
              >
                <Loader2
                  v-if="changingMap === server.id"
                  class="h-3 w-3 animate-spin"
                />
                {{ t("pages.settings.application.servers_section.change_map") }}
              </Button>
            </div>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>
