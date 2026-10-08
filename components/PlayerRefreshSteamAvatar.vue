<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { RefreshCw } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Spinner } from "~/components/ui/spinner";
import { toast } from "~/components/ui/toast";
import { e_player_roles_enum } from "~/generated/zeus";
import { useAuthStore } from "~/stores/AuthStore";

const props = defineProps<{
  steamId: string;
}>();

const emit = defineEmits<{
  refreshed: [];
}>();

const { t } = useI18n();
const auth = useAuthStore();
const busy = ref(false);

const canRefresh = computed(() =>
  auth.isRoleAbove(e_player_roles_enum.administrator),
);

async function refresh() {
  if (!canRefresh.value || busy.value) return;
  busy.value = true;
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain as string;
    const result = await $fetch<{ updated: number; checked: number }>(
      `https://${apiDomain}/avatars/admin/refresh-steam`,
      {
        method: "POST",
        credentials: "include",
        body: { steam_id: props.steamId },
      },
    );
    toast({
      title: t("pages.players.detail.refresh_avatar_done", {
        updated: result.updated,
      }),
    });
    emit("refreshed");
  } catch (error: any) {
    toast({
      variant: "destructive",
      title:
        error?.data?.message ||
        error?.message ||
        t("pages.players.detail.refresh_avatar_failed"),
    });
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <Button
    v-if="canRefresh"
    type="button"
    variant="outline"
    size="sm"
    class="w-full"
    :disabled="busy"
    @click="refresh"
  >
    <Spinner v-if="busy" class="mr-1 h-4 w-4" />
    <RefreshCw v-else class="mr-1 h-4 w-4" />
    {{ $t("pages.players.detail.refresh_avatar") }}
  </Button>
</template>
