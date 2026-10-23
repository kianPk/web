<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  ArrowDown,
  ArrowUp,
  Loader2,
  Plus,
  Settings2,
  Trash2,
} from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { toast } from "@/components/ui/toast";
import { SERVER_MODES, type ServerModeKey } from "~/utilities/serverModes";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

const props = defineProps<{
  clubId: string;
}>();

type ClubServer = {
  id: string;
  label: string | null;
  section_mode: string | null;
  connected: boolean;
  enabled: boolean;
  link: string | null;
  hosted_id: string | null;
};

type MapEntry = { id: string; name: string };

const MAP_ID = /^(\d{6,12}|[a-z][a-z0-9_]{1,63})$/;
const MAX_PER_MODE = 10;

const { t } = useI18n();
const loading = ref(true);
const error = ref("");
const saving = ref("");
const changingMap = ref("");
const servers = ref<ClubServer[]>([]);
const mapChoice = ref<Record<string, string>>({});
const counts = ref<Record<string, number>>({});
const pools = ref<Record<string, MapEntry[]>>({});

const serversByMode = computed(() => {
  const number = (label: string | null) =>
    Number(/#(\d+)\s*$/.exec(label || "")?.[1] ?? Number.MAX_SAFE_INTEGER);
  return Object.fromEntries(
    SERVER_MODES.map((mode) => [
      mode.key,
      servers.value
        .filter((server) => server.section_mode === mode.key)
        .sort((a, b) => number(a.label) - number(b.label)),
    ]),
  ) as Record<ServerModeKey, ClubServer[]>;
});

function statusOf(server: ClubServer) {
  if (!server.enabled) return "disabled";
  if (server.connected) return "online";
  return "booting";
}

function explain(message: string) {
  if (message === "No free game server slot") {
    return t("pages.clubs.servers_no_slot");
  }
  return message;
}

function apply(data: {
  servers?: ClubServer[];
  config?: Record<string, { count?: number; maps?: MapEntry[] }>;
  provision_error?: string | null;
}) {
  servers.value = data.servers ?? [];
  for (const mode of SERVER_MODES) {
    const entry = data.config?.[mode.key];
    const running = servers.value.filter(
      (server) => server.section_mode === mode.key,
    ).length;
    counts.value[mode.key] = Number.isFinite(Number(entry?.count))
      ? Number(entry?.count)
      : running;
    const maps = (entry?.maps || []).filter((map) => map?.id || map?.name);
    pools.value[mode.key] = (
      maps.length ? maps : [{ id: "", name: "" }]
    ).map((map) => ({ id: map.id || "", name: map.name || "" }));
  }
  error.value = data.provision_error ? explain(data.provision_error) : "";
}

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const data = await $fetch<{
      servers: ClubServer[];
      config?: Record<string, { count?: number; maps?: MapEntry[] }>;
      provision_error?: string | null;
    }>(`https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers`, {
      credentials: "include",
    });
    apply(data);
  } catch (err: any) {
    error.value = explain(err?.data?.message || err?.message || String(err));
  } finally {
    loading.value = false;
  }
}

function addMap(mode: ServerModeKey) {
  pools.value[mode] = [...(pools.value[mode] || []), { id: "", name: "" }];
}

function removeMap(mode: ServerModeKey, index: number) {
  const next = [...(pools.value[mode] || [])];
  next.splice(index, 1);
  pools.value[mode] = next.length ? next : [{ id: "", name: "" }];
}

function moveMap(mode: ServerModeKey, index: number, delta: number) {
  const next = [...(pools.value[mode] || [])];
  const target = index + delta;
  if (target < 0 || target >= next.length) return;
  const [row] = next.splice(index, 1);
  next.splice(target, 0, row);
  pools.value[mode] = next;
}

async function save(mode: ServerModeKey) {
  const maps = (pools.value[mode] || [])
    .map((map) => ({ id: map.id.trim(), name: map.name.trim() }))
    .filter((map) => map.id || map.name);
  if (
    maps.length === 0 ||
    maps.some((map) => !map.name || !MAP_ID.test(map.id))
  ) {
    toast({
      title: t("pages.settings.application.servers_section.map_invalid"),
      variant: "destructive",
    });
    return;
  }
  saving.value = mode;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const count = Math.min(
      MAX_PER_MODE,
      Math.max(0, Math.floor(Number(counts.value[mode]) || 0)),
    );
    const data = await $fetch<{
      servers: ClubServer[];
      config?: Record<string, { count?: number; maps?: MapEntry[] }>;
      provision_error?: string | null;
    }>(
      `https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers/settings`,
      {
        method: "POST",
        credentials: "include",
        body: { mode, count, maps },
      },
    );
    apply(data);
    if (!data.provision_error) {
      toast({ title: t("pages.clubs.servers_saved") });
    }
  } catch (err: any) {
    error.value = explain(err?.data?.message || err?.message || String(err));
    toast({ title: t("pages.clubs.servers_save_failed"), variant: "destructive" });
  } finally {
    saving.value = "";
  }
}

async function changeMap(server: ClubServer, mode: ServerModeKey) {
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
    toast({
      title: t("pages.settings.application.servers_section.change_map_sent", {
        server: server.label || mode,
      }),
    });
  } catch (err: any) {
    error.value = explain(err?.data?.message || err?.message || String(err));
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

    <template v-else>
    <article
      v-for="mode in SERVER_MODES"
      :key="mode.key"
      class="space-y-4 rounded-xl border border-border/70 bg-card/40 p-4"
    >
      <div class="flex items-center justify-between gap-3">
        <h3 class="text-base font-semibold">
          {{ t(`pages.servers.modes.${mode.key}.name`) }}
        </h3>
        <span class="font-mono text-xs text-muted-foreground">
          {{ serversByMode[mode.key].length }}/{{ counts[mode.key] || 0 }}
        </span>
      </div>

      <label class="block space-y-1.5">
        <span class="text-xs text-muted-foreground">
          {{ t("pages.settings.application.servers_section.count_hint") }}
        </span>
        <Input
          v-model="counts[mode.key]"
          type="number"
          min="0"
          :max="MAX_PER_MODE"
          class="h-9 w-28"
        />
      </label>

      <div class="space-y-2">
        <p class="text-sm font-semibold">
          {{ t("pages.settings.application.servers_section.maps") }}
        </p>
        <p class="text-xs text-muted-foreground">
          {{ t("pages.settings.application.servers_section.maps_hint") }}
        </p>
        <div
          v-for="(map, index) in pools[mode.key] || []"
          :key="index"
          class="flex items-center gap-1"
        >
          <span class="w-4 shrink-0 text-xs text-muted-foreground">{{
            index + 1
          }}</span>
          <Input
            v-model="map.name"
            :placeholder="
              t('pages.settings.application.servers_section.map_name')
            "
            class="h-8 min-w-0 flex-1 text-xs"
            dir="ltr"
          />
          <Input
            v-model="map.id"
            :placeholder="
              t('pages.settings.application.servers_section.map_id')
            "
            class="h-8 w-28 shrink-0 font-mono text-xs"
            dir="ltr"
          />
          <Button
            variant="ghost"
            size="icon"
            class="h-7 w-7 shrink-0"
            :disabled="index === 0"
            @click="moveMap(mode.key, index, -1)"
          >
            <ArrowUp class="h-3.5 w-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            class="h-7 w-7 shrink-0"
            :disabled="index === (pools[mode.key]?.length || 1) - 1"
            @click="moveMap(mode.key, index, 1)"
          >
            <ArrowDown class="h-3.5 w-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            class="h-7 w-7 shrink-0 text-destructive"
            @click="removeMap(mode.key, index)"
          >
            <Trash2 class="h-3.5 w-3.5" />
          </Button>
        </div>
        <Button
          variant="outline"
          size="sm"
          @click="addMap(mode.key)"
        >
          <Plus class="h-3.5 w-3.5" />
          {{ t("pages.settings.application.servers_section.map_add") }}
        </Button>
      </div>

      <div class="flex justify-end">
        <Button :disabled="saving === mode.key" @click="save(mode.key)">
          <Loader2 v-if="saving === mode.key" class="h-4 w-4 animate-spin" />
          {{ t("pages.settings.application.servers_section.save") }}
        </Button>
      </div>

      <div class="space-y-2 border-t border-border/60 pt-3">
        <p class="text-sm font-semibold">
          {{ t("pages.settings.application.servers_section.fleet") }}
        </p>
        <p
          v-if="serversByMode[mode.key].length === 0"
          class="text-xs text-muted-foreground"
        >
          {{ t("pages.settings.application.servers_section.none") }}
        </p>
        <ul class="space-y-2">
          <li v-for="server in serversByMode[mode.key]" :key="server.id">
            <div class="flex flex-wrap items-center gap-2 text-xs">
              <span
                class="h-2 w-2 shrink-0 rounded-full"
                :class="{
                  'bg-emerald-500': statusOf(server) === 'online',
                  'bg-amber-500': statusOf(server) === 'booting',
                  'bg-muted-foreground/40': statusOf(server) === 'disabled',
                }"
              />
              <span class="min-w-0 flex-1 truncate">{{ server.label || server.id }}</span>
              <span class="text-muted-foreground">
                {{
                  t(
                    `pages.settings.application.servers_section.status.${statusOf(server)}`,
                  )
                }}
              </span>
              <NuxtLink
                v-if="server.hosted_id"
                :to="`/hosting/${server.hosted_id}`"
                :class="[
                  tacticalCtaButtonClasses,
                  '!h-7 !gap-1 !px-2.5 !text-[0.65rem]',
                ]"
              >
                <Settings2 class="h-3 w-3" />
                {{ t("pages.clubs.servers_admin_panel") }}
              </NuxtLink>
              <a
                v-if="server.link"
                :href="server.link"
                class="rounded-md border border-border px-2 py-1"
              >
                {{ t("pages.clubs.servers_connect") }}
              </a>
            </div>
            <div
              v-if="server.enabled"
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
                  v-for="map in (pools[mode.key] || []).filter((row) => row.id)"
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
                @click="changeMap(server, mode.key)"
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
    </article>
    </template>
  </section>
</template>
