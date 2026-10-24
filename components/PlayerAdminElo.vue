<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import { Spinner } from "~/components/ui/spinner";
import { toast } from "~/components/ui/toast";
import PlayerSiteLevel from "~/components/PlayerSiteLevel.vue";
import { e_player_roles_enum } from "~/generated/zeus";
import { useAuthStore } from "~/stores/AuthStore";

const props = defineProps<{
  player: {
    steam_id: string;
    elo?: Record<string, number | null> | null;
  };
}>();

const emit = defineEmits<{
  saved: [];
}>();

const { t } = useI18n();
const auth = useAuthStore();

const types = [
  { key: "Competitive", labelKey: "pages.leaderboard.match_types.competitive" },
  { key: "Rush", labelKey: "pages.leaderboard.match_types.rush" },
  { key: "Wingman", labelKey: "pages.leaderboard.match_types.wingman" },
  { key: "Duel", labelKey: "pages.leaderboard.match_types.duel" },
] as const;

const selectedType = ref<(typeof types)[number]["key"]>("Competitive");
const eloValue = ref("");
const saving = ref(false);
const siteLevel = ref(1);
const siteXp = ref(0);
const levelValue = ref("1");
const savingLevel = ref(false);

const canEdit = computed(() =>
  auth.isRoleAbove(e_player_roles_enum.administrator),
);

function currentForType(type: string): number | null {
  const elo = props.player?.elo;
  if (!elo || typeof elo !== "object") return null;
  const key = type.toLowerCase();
  const raw = (elo as Record<string, number | null>)[key];
  if (raw == null || !Number.isFinite(Number(raw))) return null;
  return Math.round(Number(raw));
}

watch(
  [() => props.player?.elo, selectedType],
  () => {
    const cur = currentForType(selectedType.value);
    eloValue.value = cur != null ? String(cur) : "5000";
  },
  { immediate: true },
);

watch(
  () => props.player?.steam_id,
  async (steamId) => {
    if (!steamId || !canEdit.value) return;
    try {
      const apiDomain = useRuntimeConfig().public.apiDomain as string;
      const data = await $fetch<{
        players: Array<{ level: number; xp: number }>;
      }>(`https://${apiDomain}/hosted-servers/site-xp?steam_ids=${steamId}`);
      const row = data?.players?.[0];
      siteLevel.value = row?.level ?? 1;
      siteXp.value = row?.xp ?? 0;
      levelValue.value = String(siteLevel.value);
    } catch {
      siteLevel.value = 1;
      siteXp.value = 0;
      levelValue.value = "1";
    }
  },
  { immediate: true },
);

async function save() {
  if (!canEdit.value || saving.value) return;
  const elo = Math.round(Number(eloValue.value));
  if (!Number.isFinite(elo) || elo < 0 || elo > 100_000) {
    toast({
      variant: "destructive",
      title: t("pages.players.detail.admin_elo_invalid"),
    });
    return;
  }

  saving.value = true;
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain as string;
    await $fetch(`https://${apiDomain}/avatars/admin/player-elo`, {
      method: "POST",
      credentials: "include",
      body: {
        steam_id: String(props.player.steam_id),
        type: selectedType.value,
        elo,
      },
    });
    toast({ title: t("pages.players.detail.admin_elo_saved") });
    emit("saved");
  } catch (error: any) {
    const detail =
      error?.data?.message ||
      error?.data?.error ||
      (Array.isArray(error?.data?.message)
        ? error.data.message.join(", ")
        : null) ||
      error?.statusMessage ||
      error?.message ||
      t("pages.players.detail.admin_elo_failed");
    toast({
      variant: "destructive",
      title: String(detail),
    });
  } finally {
    saving.value = false;
  }
}

async function saveLevel() {
  if (!canEdit.value || savingLevel.value) return;
  const level = Math.round(Number(levelValue.value));
  if (!Number.isFinite(level) || level < 1 || level > 10) {
    toast({
      variant: "destructive",
      title: t("pages.players.detail.admin_level_failed"),
    });
    return;
  }
  savingLevel.value = true;
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain as string;
    const data = await $fetch<{
      player: { xp: number; level: number };
    }>(`https://${apiDomain}/avatars/admin/player-level`, {
      method: "POST",
      credentials: "include",
      body: { steam_id: String(props.player.steam_id), level },
    });
    siteLevel.value = data?.player?.level ?? level;
    siteXp.value = data?.player?.xp ?? siteXp.value;
    levelValue.value = String(siteLevel.value);
    toast({ title: t("pages.players.detail.admin_level_saved") });
    emit("saved");
  } catch (error: any) {
    toast({
      variant: "destructive",
      title:
        error?.data?.message ||
        error?.message ||
        t("pages.players.detail.admin_level_failed"),
    });
  } finally {
    savingLevel.value = false;
  }
}
</script>

<template>
  <div v-if="canEdit" class="space-y-3">
    <div class="grid grid-cols-1 gap-2 sm:grid-cols-2">
      <Select v-model="selectedType">
        <SelectTrigger class="h-9">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem v-for="row in types" :key="row.key" :value="row.key">
            {{ $t(row.labelKey) }}
          </SelectItem>
        </SelectContent>
      </Select>
      <Input
        v-model="eloValue"
        type="number"
        min="0"
        max="100000"
        step="1"
        class="h-9 font-mono"
        :placeholder="$t('pages.players.detail.admin_elo_placeholder')"
      />
    </div>
    <Button
      type="button"
      variant="tactical"
      size="sm"
      class="w-full"
      :disabled="saving"
      :loading="saving"
      @click="save"
    >
      <Spinner v-if="saving" class="mr-1 h-4 w-4" />
      {{ $t("pages.players.detail.admin_elo_save") }}
    </Button>

    <div class="space-y-2 border-t border-border/60 pt-3">
      <div class="flex items-center gap-2">
        <PlayerSiteLevel :level="siteLevel" :xp="siteXp" size="md" />
        <span class="font-mono text-[0.65rem] uppercase tracking-[0.16em] text-muted-foreground">
          {{ $t("pages.players.detail.admin_level_title") }}
        </span>
      </div>
      <select
        v-model="levelValue"
        class="h-9 w-full rounded-md border border-input bg-background px-3 font-mono text-sm"
      >
        <option v-for="n in 10" :key="n" :value="String(n)">
          {{ $t("pages.players.detail.site_level_now", { level: n }) }}
        </option>
      </select>
      <Button
        type="button"
        variant="tactical"
        size="sm"
        class="w-full"
        :disabled="savingLevel"
        :loading="savingLevel"
        @click="saveLevel"
      >
        <Spinner v-if="savingLevel" class="mr-1 h-4 w-4" />
        {{ $t("pages.players.detail.admin_level_save") }}
      </Button>
    </div>
  </div>
</template>
