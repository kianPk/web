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
    await $fetch(`https://${apiDomain}/matches/admin/player-elo`, {
      method: "POST",
      credentials: "include",
      body: {
        steam_id: props.player.steam_id,
        type: selectedType.value,
        elo,
      },
    });
    toast({ title: t("pages.players.detail.admin_elo_saved") });
    emit("saved");
  } catch (error: any) {
    toast({
      variant: "destructive",
      title:
        error?.data?.message ||
        error?.message ||
        t("pages.players.detail.admin_elo_failed"),
    });
  } finally {
    saving.value = false;
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
  </div>
</template>
