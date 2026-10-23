<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Loader2, Settings2 } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { toast } from "@/components/ui/toast";
import { useAuthStore } from "~/stores/AuthStore";
import { e_player_roles_enum } from "~/generated/zeus";
import { SERVER_MODES, type ServerModeKey } from "~/utilities/serverModes";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

const props = defineProps<{
  clubId: string;
}>();

const auth = useAuthStore();
const isSiteAdmin = computed(
  () => auth.me?.role === e_player_roles_enum.administrator,
);

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

const MAX_PER_MODE = 10;

const { t } = useI18n();
const loading = ref(true);
const error = ref("");
const saving = ref("");
const servers = ref<ClubServer[]>([]);
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
    pools.value[mode.key] = maps.map((map) => ({
      id: map.id || "",
      name: map.name || "",
    }));
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

async function saveCount(mode: ServerModeKey) {
  if (!isSiteAdmin.value) return;
  const maps = (pools.value[mode] || [])
    .map((map) => ({ id: map.id.trim(), name: map.name.trim() }))
    .filter((map) => map.id && map.name);
  if (!maps.length) {
    toast({
      title: t("pages.clubs.servers_maps_needed"),
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
    toast({
      title: t("pages.clubs.servers_save_failed"),
      variant: "destructive",
    });
  } finally {
    saving.value = "";
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
        class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4"
      >
        <div class="flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">
            {{ t(`pages.servers.modes.${mode.key}.name`) }}
          </h3>
          <span class="font-mono text-xs text-muted-foreground">
            {{ serversByMode[mode.key].length }}/{{ counts[mode.key] || 0 }}
          </span>
        </div>

        <div
          v-if="isSiteAdmin"
          class="flex flex-wrap items-end gap-2 rounded-lg border border-border/50 bg-background/30 p-3"
        >
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
          <Button
            size="sm"
            :disabled="saving === mode.key"
            @click="saveCount(mode.key)"
          >
            <Loader2 v-if="saving === mode.key" class="h-4 w-4 animate-spin" />
            {{ t("pages.settings.application.servers_section.save") }}
          </Button>
        </div>

        <p
          v-if="serversByMode[mode.key].length === 0"
          class="text-xs text-muted-foreground"
        >
          {{ t("pages.settings.application.servers_section.none") }}
        </p>
        <ul class="space-y-2">
          <li
            v-for="server in serversByMode[mode.key]"
            :key="server.id"
            class="flex flex-wrap items-center gap-2 text-xs"
          >
            <span
              class="h-2 w-2 shrink-0 rounded-full"
              :class="{
                'bg-emerald-500': statusOf(server) === 'online',
                'bg-amber-500': statusOf(server) === 'booting',
                'bg-muted-foreground/40': statusOf(server) === 'disabled',
              }"
            />
            <span class="min-w-0 flex-1 truncate">{{
              server.label || server.id
            }}</span>
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
          </li>
        </ul>
      </article>
    </template>
  </section>
</template>
