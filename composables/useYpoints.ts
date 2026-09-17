import { computed, onMounted, ref, watch } from "vue";
import { useApplicationSettingsStore } from "~/stores/ApplicationSettings";
import { useAuthStore } from "~/stores/AuthStore";

export type YpointCosts = {
  duel: number;
  wingman: number;
  draft_create: number;
  draft_join: number;
};

const balance = ref<number | null>(null);
const costs = ref<YpointCosts>({
  duel: 5,
  wingman: 8,
  draft_create: 10,
  draft_join: 10,
});
const loading = ref(false);
let loadedOnce = false;

function settingCost(name: string, fallback: number) {
  const raw = useApplicationSettingsStore().settings?.find(
    (s) => s.name === name,
  )?.value;
  const num = Number(raw);
  return Number.isFinite(num) ? Math.max(0, num) : fallback;
}

export function useYpoints() {
  const auth = useAuthStore();

  const syncCostsFromSettings = () => {
    costs.value = {
      duel: settingCost("public.ypoint_cost_duel", costs.value.duel),
      wingman: settingCost("public.ypoint_cost_wingman", costs.value.wingman),
      draft_create: settingCost(
        "public.ypoint_cost_draft_create",
        costs.value.draft_create,
      ),
      draft_join: settingCost(
        "public.ypoint_cost_draft_join",
        costs.value.draft_join,
      ),
    };
  };

  const refresh = async () => {
    syncCostsFromSettings();
    if (!auth.me?.steam_id) {
      balance.value = null;
      return;
    }
    loading.value = true;
    try {
      const apiDomain = useRuntimeConfig().public.apiDomain as string;
      const data = await $fetch<{
        balance: number;
        costs: YpointCosts;
      }>(`https://${apiDomain}/ypoint/me`, {
        credentials: "include",
      });
      balance.value = Number(data.balance ?? 0);
      if (data.costs) {
        costs.value = {
          duel: Math.max(0, Number(data.costs.duel) || 0),
          wingman: Math.max(0, Number(data.costs.wingman) || 0),
          draft_create: Math.max(0, Number(data.costs.draft_create) || 0),
          draft_join: Math.max(0, Number(data.costs.draft_join) || 0),
        };
      }
    } catch (error) {
      console.error("Failed to load Ypoints", error);
      syncCostsFromSettings();
    } finally {
      loading.value = false;
      loadedOnce = true;
    }
  };

  const costForMatchType = (type: string) => {
    if (type === "Duel") return costs.value.duel;
    if (type === "Wingman") return costs.value.wingman;
    return 0;
  };

  const canAfford = (amount: number) => {
    if (amount <= 0) return true;
    if (balance.value === null) return true;
    return balance.value >= amount;
  };

  if (import.meta.client && !loadedOnce) {
    onMounted(() => {
      void refresh();
    });
    watch(
      () => auth.me?.steam_id,
      () => {
        void refresh();
      },
    );
  }

  return {
    balance: computed(() => balance.value),
    costs: computed(() => costs.value),
    loading: computed(() => loading.value),
    refresh,
    costForMatchType,
    canAfford,
  };
}
