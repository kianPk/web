<script setup lang="ts">
import { watch, ref, onMounted, onBeforeUnmount, computed, defineAsyncComponent } from "vue";
import { useAuthStore } from "~/stores/AuthStore";
import { usePluginsStore } from "~/stores/Plugins";
import LoadingScreen from "~/components/LoadingScreen.vue";
import { afterReveal } from "~/utils/afterReveal";

// Keep the landing chunk off the critical path — cuts TBT on cold guest load.
const GuestLandingPage = defineAsyncComponent(
  () => import("~/components/landing/GuestLandingPage.vue"),
);

definePageMeta({
  layout: "landing",
});

const authStore = useAuthStore();
const pluginsStore = usePluginsStore();
const pluginsDeadlinePassed = ref(pluginsStore.initialized);
let pluginsDeadlineTimer: ReturnType<typeof setTimeout> | undefined;

onMounted(() => {
  pluginsDeadlineTimer = setTimeout(() => {
    pluginsDeadlinePassed.value = true;
  }, 2000);

  // Session check after first paint — guest landing already shows.
  afterReveal(() => {
    if (!authStore.hasCheckedSession) {
      void authStore.getMe();
    }
  });
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

const showGuestLanding = computed(() => {
  // Unknown session → treat as guest so the landing paints immediately.
  if (!authStore.hasCheckedSession) {
    return true;
  }
  return !authStore.me?.steam_id;
});

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
  <!-- Optimistic guest landing: don't wait on getMe() or lab tools see only a
       spinner forever (ssr:false + session gate caused PageSpeed NO_FCP). -->
  <GuestLandingPage v-if="showGuestLanding" />
  <LoadingScreen v-else class="min-h-screen" />
</template>
