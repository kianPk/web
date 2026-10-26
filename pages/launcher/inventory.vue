<script setup lang="ts">
import { ref } from "vue";
import PluginRemote from "~/components/plugins/PluginRemote.vue";

definePageMeta({ layout: "launcher", keepalive: true });
useHead({ title: "Inventory" });

const invPath = ref("/");
const invQuery = ref<Record<string, unknown>>({});

function invNavigate(
  to: string,
  options: { replace?: boolean; query?: Record<string, unknown> } = {},
) {
  invPath.value =
    (to.startsWith("/") ? to : `/${to}`).replace(/\/$/, "") || "/";
  if (options.query) invQuery.value = options.query;
}
</script>

<template>
  <div class="inventory-glass flex h-full min-h-0 w-full flex-col overflow-hidden">
    <div class="min-h-0 flex-1 overflow-auto p-3 sm:p-4">
      <PluginRemote
        slug="inventory"
        base="/launcher/inventory"
        :path="invPath"
        :query="invQuery"
        :navigate="invNavigate"
      />
    </div>
  </div>
</template>

<style scoped>
.inventory-glass {
  min-height: 100%;
  background: linear-gradient(
    165deg,
    rgba(255, 255, 255, 0.08) 0%,
    rgba(255, 255, 255, 0.025) 42%,
    rgba(12, 13, 16, 0.5) 100%
  );
  backdrop-filter: blur(28px);
  -webkit-backdrop-filter: blur(28px);
}
</style>
