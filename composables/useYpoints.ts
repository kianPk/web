import { computed, onMounted, ref, watch } from "vue";
import { useApplicationSettingsStore } from "~/stores/ApplicationSettings";
import { useAuthStore } from "~/stores/AuthStore";

export type YpointCosts = {
  duel: number;
  wingman: number;
  trios: number;
  draft_create: number;
  draft_join: number;
};

const balance = ref<number | null>(null);
const costs = ref<YpointCosts>({
  duel: 8,
  wingman: 0,
  trios: 12,
  draft_create: 15,
  draft_join: 10,
});
const loading = ref(false);
let refreshPromise: Promise<void> | null = null;
let watchersBound = false;

function settingCost(name: string, fallback: number) {
  const raw = useApplicationSettingsStore().settings?.find(
    (s) => s.name === name,
  )?.value;
  const num = Number(raw);
  return Number.isFinite(num) ? Math.max(0, num) : fallback;
}

function rankedFreeFromSettings() {
  const raw = useApplicationSettingsStore().settings?.find(
    (s) => s.name === "public.ypoint_ranked_free",
  )?.value;
  return raw === "true" || raw === "1";
}

function applyCosts(next: Partial<YpointCosts> | null | undefined) {
  if (!next) return;
  costs.value = {
    duel: Math.max(0, Number(next.duel) || 0),
    wingman: Math.max(0, Number(next.wingman) || 0),
    trios: Math.max(0, Number(next.trios) || 0),
    draft_create: Math.max(0, Number(next.draft_create) || 0),
    draft_join: Math.max(0, Number(next.draft_join) || 0),
  };
}

export function useYpoints() {
  const auth = useAuthStore();
  const settingsStore = useApplicationSettingsStore();

  const syncCostsFromSettings = () => {
    const rankedFree = rankedFreeFromSettings();
    costs.value = {
      duel: rankedFree ? 0 : settingCost("public.ypoint_cost_duel", costs.value.duel),
      wingman: rankedFree
        ? 0
        : settingCost("public.ypoint_cost_wingman", costs.value.wingman),
      trios: rankedFree
        ? 0
        : settingCost("public.ypoint_cost_trios", costs.value.trios),
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
    if (refreshPromise) return refreshPromise;
    refreshPromise = (async () => {
      syncCostsFromSettings();
      loading.value = true;
      try {
        const apiDomain = useRuntimeConfig().public.apiDomain as string;

        // Public costs — always refresh so price changes show without login.
        try {
          const publicCosts = await $fetch<YpointCosts>(
            `https://${apiDomain}/ypoint/costs`,
            { credentials: "include" },
          );
          applyCosts(publicCosts);
        } catch (error) {
          console.error("Failed to load Ypoint costs", error);
        }

        if (!auth.me?.steam_id) {
          balance.value = null;
          return;
        }

        const data = await $fetch<{
          balance: number;
          costs: YpointCosts;
        }>(`https://${apiDomain}/ypoint/me`, {
          credentials: "include",
        });
        balance.value = Number(data.balance ?? 0);
        applyCosts(data.costs);
      } catch (error) {
        console.error("Failed to load Ypoints", error);
        syncCostsFromSettings();
      } finally {
        loading.value = false;
        refreshPromise = null;
      }
    })();
    return refreshPromise;
  };

  const costForMatchType = (type: string) => {
    if (type === "Duel") return costs.value.duel;
    if (type === "Wingman") return costs.value.wingman;
    if (type === "Trios") return costs.value.trios;
    return 0;
  };

  const canAfford = (amount: number) => {
    if (amount <= 0) return true;
    if (balance.value === null) return true;
    return balance.value >= amount;
  };

  if (import.meta.client && !watchersBound) {
    watchersBound = true;
    onMounted(() => {
      void refresh();
    });
    watch(
      () => auth.me?.steam_id,
      () => {
        void refresh();
      },
    );
    // When Hasura settings subscription delivers new prices, pick them up.
    watch(
      () =>
        settingsStore.settings
          ?.filter(
            (s) =>
              s.name.startsWith("public.ypoint_cost_") ||
              s.name === "public.ypoint_ranked_free",
          )
          .map((s) => `${s.name}:${s.value}`)
          .join("|") ?? "",
      () => {
        syncCostsFromSettings();
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
