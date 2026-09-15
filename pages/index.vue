<script setup lang="ts">
import { watch, ref, onMounted, onBeforeUnmount } from "vue";
import { useAuthStore } from "~/stores/AuthStore";
import { usePluginsStore } from "~/stores/Plugins";
import LoadingScreen from "~/components/LoadingScreen.vue";

definePageMeta({
  layout: "public",
});

const authStore = useAuthStore();
const pluginsStore = usePluginsStore();
// Don't wait forever for the plugin registry WS — proceed to /me or /watch.
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

// A plugin flagged is_default takes over the landing route, but only once
// the registry has loaded and only if the current viewer may see it — otherwise
// fall back to the normal /me vs /watch redirect.
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
    if (!hasCheckedSession) {
      return;
    }
    if (!initialized && !deadlinePassed) {
      return;
    }

    if (defaultPlugin) {
      void navigateTo(`/apps/${defaultPlugin.slug}`, { replace: true });
      return;
    }

    void navigateTo(steamId ? "/me" : "/watch", {
      replace: true,
    });
  },
  { immediate: true },
);
</script>

<template>
  <LoadingScreen class="min-h-screen" />
</template>
