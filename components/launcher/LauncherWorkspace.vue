<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { Headset, Copy, ExternalLink } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { toast } from "~/components/ui/toast";
import PluginRemote from "~/components/plugins/PluginRemote.vue";

const props = defineProps<{
  active: string;
}>();

const { t } = useI18n();

/** Mount once, then only show/hide — no page flash between tabs. */
const visited = reactive<Record<string, boolean>>({
  play: true,
});

watch(
  () => props.active,
  (id) => {
    if (id) visited[id] = true;
  },
  { immediate: true },
);

type EmbedPanel = {
  id: string;
  src: string;
};

const embedPanels: EmbedPanel[] = [
  { id: "play", src: "/play?embed=1" },
  { id: "servers", src: "/game-servers?embed=1" },
  { id: "battle-pass", src: "/battle-pass?embed=1" },
  { id: "clubs", src: "/clubs?embed=1" },
  { id: "scrims", src: "/scrims?embed=1" },
];

const loaded = reactive<Record<string, boolean>>({});

function markLoaded(id: string) {
  loaded[id] = true;
}

// —— Inventory: mount plugin in-shell (no nested iframe) ——
const invPath = ref("/");
const invQuery = ref<Record<string, unknown>>({});

function invNavigate(
  to: string,
  options: { replace?: boolean; query?: Record<string, unknown> } = {},
) {
  invPath.value = (to.startsWith("/") ? to : `/${to}`).replace(/\/$/, "") || "/";
  if (options.query) {
    invQuery.value = options.query;
  }
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

const showInventory = computed(() => props.active === "inventory");
const showTeamspeak = computed(() => props.active === "teamspeak");
</script>

<template>
  <div class="relative h-full w-full overflow-hidden">
    <!-- Site embeds: keep-alive via v-show after first visit -->
    <div
      v-for="panel in embedPanels"
      v-show="active === panel.id"
      :key="panel.id"
      class="absolute inset-0"
    >
      <template v-if="visited[panel.id]">
        <div
          v-if="!loaded[panel.id]"
          class="pointer-events-none absolute inset-0 z-10 animate-pulse bg-white/[0.03]"
        />
        <iframe
          :src="panel.src"
          class="absolute inset-0 h-full w-full border-0 bg-transparent"
          :title="panel.id"
          allow="clipboard-read; clipboard-write"
          @load="markLoaded(panel.id)"
        />
      </template>
    </div>

    <!-- Inventory: glass host + native plugin (not a second browser page) -->
    <div
      v-show="showInventory"
      class="absolute inset-0 overflow-hidden"
    >
      <div
        v-if="visited.inventory"
        class="inventory-glass flex h-full min-h-0 w-full flex-col overflow-hidden"
      >
        <div class="min-h-0 flex-1 overflow-auto">
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

    <!-- TeamSpeak: native glass card -->
    <div
      v-show="showTeamspeak"
      class="absolute inset-0 flex items-center justify-center overflow-auto p-6 sm:p-10"
    >
      <div
        v-if="visited.teamspeak"
        class="w-full max-w-lg rounded-3xl border border-white/10 bg-white/[0.06] p-7 text-center shadow-[0_24px_80px_rgba(0,0,0,0.35)] backdrop-blur-2xl sm:p-10"
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
  background:
    linear-gradient(
      165deg,
      rgba(255, 255, 255, 0.07) 0%,
      rgba(255, 255, 255, 0.02) 40%,
      rgba(12, 13, 16, 0.55) 100%
    );
  backdrop-filter: blur(28px);
  -webkit-backdrop-filter: blur(28px);
}

.inventory-glass :deep(> div) {
  min-height: 100%;
}

/* Soften opaque plugin chrome so it sits in the glass shell */
.inventory-glass :deep(body),
.inventory-glass :deep([class*="bg-background"]),
.inventory-glass :deep([class*="bg-card"]) {
  background-color: transparent !important;
}
</style>
