import { onMounted, ref, watch } from "vue";
import { useAuthStore } from "~/stores/AuthStore";

/** Site Toman wallet balance, stored/returned as Rials (IRR). */
const balance = ref<number | null>(null);
const loading = ref(false);
let refreshPromise: Promise<void> | null = null;
let watchersBound = false;

export function useIrrWallet() {
  const auth = useAuthStore();

  const refresh = async () => {
    if (refreshPromise) return refreshPromise;
    refreshPromise = (async () => {
      loading.value = true;
      try {
        if (!auth.me?.steam_id) {
          balance.value = null;
          return;
        }
        const apiDomain = useRuntimeConfig().public.apiDomain as string;
        const data = await $fetch<{ balance: number }>(
          `https://${apiDomain}/irr/me`,
          { credentials: "include" },
        );
        balance.value = Number(data.balance ?? 0);
      } catch (error) {
        console.error("Failed to load IRR wallet", error);
        // Keep last known value if any; otherwise show 0 so the panel isn't blank.
        if (balance.value === null) balance.value = 0;
      } finally {
        loading.value = false;
        refreshPromise = null;
      }
    })();
    return refreshPromise;
  };

  if (!watchersBound) {
    watchersBound = true;
    watch(
      () => auth.me?.steam_id,
      () => {
        void refresh();
      },
    );
  }

  onMounted(() => {
    void refresh();
  });

  return {
    balance,
    loading,
    refresh,
  };
}
