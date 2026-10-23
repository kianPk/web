<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  ArrowDown,
  ArrowUp,
  Loader2,
  Map as MapIcon,
  Plus,
  Trash2,
} from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { toast } from "@/components/ui/toast";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

const props = defineProps<{
  clubId: string;
  serverId: string;
  sectionMode: string;
  connected?: boolean;
}>();

const emit = defineEmits<{
  "maps-updated": [maps: Array<{ id: string; name: string }>];
}>();

type MapEntry = { id: string; name: string };

const MAP_ID = /^(\d{6,12}|[a-z][a-z0-9_]{1,63})$/;

const { t } = useI18n();
const loading = ref(true);
const saving = ref(false);
const changing = ref(false);
const error = ref("");
const maps = ref<MapEntry[]>([{ id: "", name: "" }]);
const mapChoice = ref<string>("");

const validMaps = computed(() =>
  maps.value.filter((row) => row.id.trim() && row.name.trim()),
);

function explain(message: string) {
  if (message === "No free game server slot") {
    return t("pages.clubs.servers_no_slot");
  }
  return message;
}

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    const data = await $fetch<{
      config?: Record<string, { count?: number; maps?: MapEntry[] }>;
    }>(`https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers`, {
      credentials: "include",
    });
    const entry = data.config?.[props.sectionMode];
    const rows = (entry?.maps || []).filter((map) => map?.id || map?.name);
    maps.value = (rows.length ? rows : [{ id: "", name: "" }]).map((map) => ({
      id: map.id || "",
      name: map.name || "",
    }));
    emit("maps-updated", validMaps.value);
  } catch (err: any) {
    error.value = explain(err?.data?.message || err?.message || String(err));
  } finally {
    loading.value = false;
  }
}

function addMap() {
  maps.value = [...maps.value, { id: "", name: "" }];
}

function removeMap(index: number) {
  const next = [...maps.value];
  next.splice(index, 1);
  maps.value = next.length ? next : [{ id: "", name: "" }];
}

function moveMap(index: number, delta: number) {
  const next = [...maps.value];
  const target = index + delta;
  if (target < 0 || target >= next.length) return;
  const [row] = next.splice(index, 1);
  next.splice(target, 0, row);
  maps.value = next;
}

async function save() {
  const cleaned = maps.value
    .map((map) => ({ id: map.id.trim(), name: map.name.trim() }))
    .filter((map) => map.id || map.name);
  if (
    cleaned.length === 0 ||
    cleaned.some((map) => !map.name || !MAP_ID.test(map.id))
  ) {
    toast({
      title: t("pages.settings.application.servers_section.map_invalid"),
      variant: "destructive",
    });
    return;
  }
  saving.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    await $fetch(
      `https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers/settings`,
      {
        method: "POST",
        credentials: "include",
        body: {
          mode: props.sectionMode,
          maps: cleaned,
        },
      },
    );
    maps.value = cleaned;
    emit("maps-updated", cleaned);
    toast({ title: t("pages.clubs.servers_maps_saved") });
  } catch (err: any) {
    error.value = explain(err?.data?.message || err?.message || String(err));
    toast({
      title: t("pages.clubs.servers_save_failed"),
      variant: "destructive",
    });
  } finally {
    saving.value = false;
  }
}

async function changeMap() {
  if (!mapChoice.value || !props.serverId) return;
  changing.value = true;
  error.value = "";
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain;
    await $fetch(
      `https://${apiDomain}/hosted-servers/clubs/${props.clubId}/servers/${props.serverId}/map`,
      {
        method: "POST",
        credentials: "include",
        body: { map: mapChoice.value },
      },
    );
    toast({
      title: t("pages.settings.application.servers_section.change_map_sent", {
        server: props.sectionMode,
      }),
    });
  } catch (err: any) {
    error.value = explain(err?.data?.message || err?.message || String(err));
  } finally {
    changing.value = false;
  }
}

watch(
  () => [props.clubId, props.sectionMode] as const,
  () => void load(),
  { immediate: true },
);
</script>

<template>
  <section class="hosted-card space-y-4 p-5 sm:p-6">
    <h2 class="m-0 flex items-center gap-2 text-base font-semibold tracking-tight">
      <span class="h-[2px] w-2.5 bg-[hsl(var(--tac-amber))]" />
      <MapIcon class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
      {{ t("pages.clubs.servers_maps_panel") }}
    </h2>
    <p class="text-xs text-muted-foreground">
      {{ t("pages.clubs.servers_maps_panel_hint") }}
    </p>
    <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
    <p v-if="loading" class="text-sm text-muted-foreground">...</p>

    <template v-else>
      <div class="space-y-2">
        <div
          v-for="(map, index) in maps"
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
            @click="moveMap(index, -1)"
          >
            <ArrowUp class="h-3.5 w-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            class="h-7 w-7 shrink-0"
            :disabled="index === maps.length - 1"
            @click="moveMap(index, 1)"
          >
            <ArrowDown class="h-3.5 w-3.5" />
          </Button>
          <Button
            variant="ghost"
            size="icon"
            class="h-7 w-7 shrink-0 text-destructive"
            @click="removeMap(index)"
          >
            <Trash2 class="h-3.5 w-3.5" />
          </Button>
        </div>
        <div class="flex flex-wrap gap-2">
          <Button variant="outline" size="sm" @click="addMap">
            <Plus class="h-3.5 w-3.5" />
            {{ t("pages.settings.application.servers_section.map_add") }}
          </Button>
          <Button
            size="sm"
            :disabled="saving"
            :class="tacticalCtaButtonClasses"
            @click="save"
          >
            <Loader2 v-if="saving" class="h-4 w-4 animate-spin" />
            {{ t("pages.settings.application.servers_section.save") }}
          </Button>
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2 border-t border-border/60 pt-4">
        <select
          v-model="mapChoice"
          class="h-9 min-w-[10rem] flex-1 rounded-md border border-border bg-background px-2 text-sm"
          dir="ltr"
        >
          <option value="" disabled>
            {{
              t("pages.settings.application.servers_section.change_map_pick")
            }}
          </option>
          <option
            v-for="map in validMaps"
            :key="map.id"
            :value="map.id"
          >
            {{ map.name || map.id }}
          </option>
        </select>
        <Button
          size="sm"
          variant="outline"
          :disabled="!mapChoice || changing || !connected"
          @click="changeMap"
        >
          <Loader2 v-if="changing" class="h-3.5 w-3.5 animate-spin" />
          {{ t("pages.settings.application.servers_section.change_map") }}
        </Button>
      </div>
    </template>
  </section>
</template>
