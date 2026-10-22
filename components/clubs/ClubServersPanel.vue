<script setup lang="ts">
import { Loader2 } from "lucide-vue-next";
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

const { t } = useI18n();
const servers = ref<ClubServer[]>([]);
const manage = ref(false);
const loading = ref(true);
const busy = ref(false);
const error = ref("");
const mapChoice = ref<Record<string, string>>({});
const changingMap = ref("");
const pools = ref<Record<string, Array<{ id: string; name: string }>>>({});

const dedicated = computed(() =>
  servers.value.length === 1 ? servers.value[0] : null,
);

function statusOf(server: ClubServer) {
  if (!server.enabled) return "disabled";
  if (server.connected) return "online";
  return "booting";
}

function serversFor(mode: ServerModeKey) {
  return servers.value.filter((server) => server.section_mode === mode);
}

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const data = await $fetch<{
      servers: ClubServer[];
      manage: boolean;
      provision_error?: string | null;
    }>(`https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers`, {
      credentials: "include",
    });
    servers.value = data.servers ?? [];
    manage.value = !!data.manage && props.canManage;
    error.value = data.provision_error || "";
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

async function runMode(mode: ServerModeKey) {
  if (!manage.value || dedicated.value?.section_mode === mode) return;
  busy.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    await $fetch(
      `https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers/mode`,
      {
        method: "POST",
        credentials: "include",
        body: { mode },
      },
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
      `https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers/${server.id}/map`,
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

    <div v-else class="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      <div
        v-for="mode in SERVER_MODES"
        :key="mode.key"
        class="rounded-xl border border-border/70 bg-card/40 p-3"
        :class="
          dedicated?.section_mode === mode.key
            ? 'border-[hsl(var(--tac-amber)/0.7)]'
            : ''
        "
      >
        <div class="mb-2 flex items-center justify-between gap-2">
          <p class="text-sm font-semibold">
            {{ t(`pages.servers.modes.${mode.key}.name`) }}
          </p>
          <Button
            v-if="manage && dedicated && dedicated.section_mode !== mode.key"
            size="sm"
            variant="outline"
            class="h-7 px-2 text-xs"
            :disabled="busy"
            @click="runMode(mode.key)"
          >
            {{ t("pages.clubs.servers_run_mode") }}
          </Button>
          <span
            v-else-if="dedicated?.section_mode === mode.key"
            class="text-xs text-[hsl(var(--tac-amber))]"
          >
            {{ t("pages.clubs.servers_active_mode") }}
          </span>
        </div>

        <p
          v-if="!serversFor(mode.key).length"
          class="text-xs text-muted-foreground"
        >
          {{ t("pages.clubs.servers_empty") }}
        </p>
        <ul class="space-y-2">
          <li v-for="server in serversFor(mode.key)" :key="server.id">
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
            </div>
            <div
              v-if="manage && server.enabled && pools[mode.key]"
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
                  v-for="map in pools[mode.key]"
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
