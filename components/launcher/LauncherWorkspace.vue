<script setup lang="ts">
import { reactive, ref, shallowRef, watch, type Component } from "vue";
import { Headset, Copy, ExternalLink } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { toast } from "~/components/ui/toast";
import PluginRemote from "~/components/plugins/PluginRemote.vue";

const props = defineProps<{
  active: string;
}>();

const { t } = useI18n();

/** In-process panels — no iframes (those were blank / broken in the client). */
const panelImports: Record<string, () => Promise<{ default: Component }>> = {
  play: () => import("~/pages/play/index.vue"),
  servers: () => import("~/pages/game-servers/index.vue"),
  "battle-pass": () => import("~/pages/battle-pass/index.vue"),
  clubs: () => import("~/pages/clubs/index.vue"),
  scrims: () => import("~/pages/scrims/index.vue"),
};

const mounted = reactive<Record<string, boolean>>({ play: true });
const components = shallowRef<Record<string, Component>>({});
const loadingPanel = ref<string | null>(null);

async function ensurePanel(id: string) {
  if (!panelImports[id]) return;
  mounted[id] = true;
  if (components.value[id]) return;
  loadingPanel.value = id;
  try {
    const mod = await panelImports[id]();
    components.value = {
      ...components.value,
      [id]: mod.default,
    };
  } finally {
    if (loadingPanel.value === id) loadingPanel.value = null;
  }
}

watch(
  () => props.active,
  (id) => {
    if (id) void ensurePanel(id);
    if (id === "inventory") mounted.inventory = true;
    if (id === "teamspeak") mounted.teamspeak = true;
  },
  { immediate: true },
);

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

const tsAddress = "yguard.asts.ir:6857";
const tsConnectUrl = "ts3server://yguard.asts.ir?port=6857";

async function copyTs() {
  try {
    await navigator.clipboard.writeText(tsAddress);
    toast({ title: t("layouts.launcher.teamspeak_copied") });
  } catch {
    toast({
      title: t("layouts.launcher.teamspeak_copy_failed"),
      variant: "destructive",
    });
  }
}

const contentIds = ["play", "servers", "battle-pass", "clubs", "scrims"] as const;
</script>

<template>
  <div class="relative h-full w-full overflow-hidden">
    <div
      v-for="id in contentIds"
      v-show="active === id"
      :key="id"
      class="absolute inset-0 overflow-x-hidden overflow-y-auto"
    >
      <div
        v-if="mounted[id]"
        class="mx-auto w-full max-w-[1400px] px-4 py-5 sm:px-6 sm:py-7 lg:px-8 lg:py-8"
      >
        <div
          v-if="loadingPanel === id && !components[id]"
          class="space-y-4"
        >
          <div class="h-40 animate-pulse rounded-2xl bg-white/[0.05]" />
          <div class="h-24 animate-pulse rounded-2xl bg-white/[0.04]" />
          <div class="h-24 animate-pulse rounded-2xl bg-white/[0.04]" />
        </div>
        <component :is="components[id]" v-else-if="components[id]" />
      </div>
    </div>

    <div v-show="active === 'inventory'" class="absolute inset-0 overflow-hidden">
      <div
        v-if="mounted.inventory"
        class="inventory-glass flex h-full min-h-0 w-full flex-col overflow-hidden"
      >
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
    </div>

    <div
      v-show="active === 'teamspeak'"
      class="absolute inset-0 flex items-center justify-center overflow-auto p-6 sm:p-10"
    >
      <div
        v-if="mounted.teamspeak"
        class="w-full max-w-lg rounded-3xl border border-white/10 bg-white/[0.07] p-7 text-center shadow-[0_24px_80px_rgba(0,0,0,0.35)] backdrop-blur-2xl sm:p-10"
      >
        <div
          class="mx-auto mb-5 grid h-16 w-16 place-items-center rounded-2xl bg-[#ff5500]/15 text-[#ff5500] ring-1 ring-[#ff5500]/25"
        >
          <Headset class="h-8 w-8" :stroke-width="1.75" />
        </div>
        <h1 class="text-xl font-semibold tracking-tight text-white">
          {{ t("layouts.app_nav.navigation.teamspeak") }}
        </h1>
        <p class="mt-2 text-sm text-white/50">
          {{ t("layouts.launcher.teamspeak_hint") }}
        </p>
        <p
          class="mt-6 rounded-xl bg-black/35 px-4 py-3 font-mono text-sm tracking-wide text-[#ff6a1f] ring-1 ring-white/5"
        >
          {{ tsAddress }}
        </p>
        <div class="mt-6 flex flex-wrap items-center justify-center gap-2">
          <Button class="bg-[#ff5500] text-white hover:bg-[#ff6a1f]" as-child>
            <a :href="tsConnectUrl">
              <ExternalLink class="h-4 w-4" />
              {{ t("layouts.launcher.teamspeak_connect") }}
            </a>
          </Button>
          <Button
            variant="outline"
            class="border-white/10 bg-transparent text-white/80 hover:bg-white/[0.06]"
            @click="copyTs"
          >
            <Copy class="h-4 w-4" />
            {{ t("layouts.launcher.teamspeak_copy") }}
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.inventory-glass {
  background: linear-gradient(
    165deg,
    rgba(255, 255, 255, 0.08) 0%,
    rgba(255, 255, 255, 0.025) 42%,
    rgba(12, 13, 16, 0.5) 100%
  );
  backdrop-filter: blur(28px);
  -webkit-backdrop-filter: blur(28px);
}

.inventory-glass :deep(> div) {
  min-height: 100%;
}
</style>
