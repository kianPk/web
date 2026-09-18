import { computed, onMounted, onUnmounted, ref, watch } from "vue";

export type AcStatus = {
  required: boolean;
  valid: boolean;
};

/**
 * Poll YGuard AC launcher status (session cookie).
 * When AC is required, lineup players must keep the launcher open to join/play.
 */
export function useAcStatus(pollMs = 15_000) {
  const required = ref(false);
  const valid = ref(true);
  const loading = ref(false);

  async function refresh() {
    const steamId = useAuthStore().me?.steam_id;
    if (!steamId) {
      required.value = false;
      valid.value = true;
      return;
    }
    loading.value = true;
    try {
      const apiDomain = useRuntimeConfig().public.apiDomain as string;
      const status = await $fetch<{ required?: boolean; valid?: boolean }>(
        `https://${apiDomain}/plugins/ac/status`,
        { credentials: "include" },
      );
      required.value = !!status?.required;
      valid.value = !!status?.valid;
    } catch {
      // Fail open if status is unreachable — server-side kick still enforces.
      required.value = false;
      valid.value = true;
    } finally {
      loading.value = false;
    }
  }

  const steamId = computed(() => useAuthStore().me?.steam_id);
  watch(steamId, () => void refresh(), { immediate: true });

  onMounted(() => {
    const id = window.setInterval(() => void refresh(), pollMs);
    onUnmounted(() => window.clearInterval(id));
  });

  return { required, valid, loading, refresh };
}
