<script setup lang="ts">
import { watch, ref, onMounted, onBeforeUnmount, computed } from "vue";
import { useAuthStore } from "~/stores/AuthStore";
import { usePluginsStore } from "~/stores/Plugins";
import LoadingScreen from "~/components/LoadingScreen.vue";
import GuestLandingPage from "~/components/landing/GuestLandingPage.vue";

definePageMeta({
  layout: "landing",
});

const authStore = useAuthStore();
const pluginsStore = usePluginsStore();
const pluginsDeadlinePassed = ref(pluginsStore.initialized);
let pluginsDeadlineTimer: ReturnType<typeof setTimeout> | undefined;

if (!authStore.hasCheckedSession) {
  void authStore.getMe();
}

onMounted(() => {
  pluginsDeadlineTimer = setTimeout(() => {
    pluginsDeadlinePassed.value = true;
  }, 2000);
});

watch(
  () => pluginsStore.initialized,
  (ready) => {
    if (ready) {
      pluginsDeadlinePassed.value = true;
    }
  },
);

onBeforeUnmount(() => {
  if (pluginsDeadlineTimer) {
    clearTimeout(pluginsDeadlineTimer);
  }
});

const showGuestLanding = computed(
  () => authStore.hasCheckedSession && !authStore.me?.steam_id,
);

// Logged-in: default plugin or /me. Guests stay here — never Inventory.
watch(
  [
    () => authStore.hasCheckedSession,
    () => authStore.me?.steam_id,
    () => pluginsStore.initialized,
    () => pluginsStore.defaultPlugin,
    () => pluginsDeadlinePassed.value,
  ],
  ([
    hasCheckedSession,
    steamId,
    initialized,
    defaultPlugin,
    deadlinePassed,
  ]) => {
    if (!hasCheckedSession || !steamId) {
      return;
    }
    if (!initialized && !deadlinePassed) {
      return;
    }

    if (defaultPlugin) {
      void navigateTo(`/apps/${defaultPlugin.slug}`, { replace: true });
      return;
    }

    void navigateTo("/me", { replace: true });
  },
  { immediate: true },
);
</script>

<template>
  <GuestLandingPage v-if="showGuestLanding" />
  <LoadingScreen v-else class="min-h-screen" />
</template>
