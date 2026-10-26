<script setup lang="ts">
import { computed, defineAsyncComponent, ref } from "vue";
import { useI18n } from "vue-i18n";
import { ArrowLeft } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import { Button } from "~/components/ui/button";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import ServerHostTile from "~/components/servers/ServerHostTile.vue";
import ServerPublicTile from "~/components/servers/ServerPublicTile.vue";
import ServersAgentsHero from "~/components/servers/ServersAgentsHero.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";

const GameServersMode = defineAsyncComponent(
  () => import("~/pages/game-servers/[mode].vue"),
);
const HostServersPage = defineAsyncComponent(
  () => import("~/pages/game-servers/host.vue"),
);
const PublicServersPage = defineAsyncComponent(
  () => import("~/pages/public-servers/index.vue"),
);

type Panel = "home" | "mode" | "host" | "public";

const panel = ref<Panel>("home");
const pickedMode = ref("");
const { loading, servers, modes } = usePublicServerFleet();
const ready = computed(() => !loading.value || servers.value.length > 0);

const { t } = useI18n();
useHead(() => ({ title: t("pages.servers.title") }));

function openMode(key: string) {
  pickedMode.value = key;
  panel.value = "mode";
}

function goHome() {
  panel.value = "home";
  pickedMode.value = "";
}
</script>

<template>
  <PageTransition :delay="0">
    <section v-if="panel === 'home'" class="space-y-6">
      <ClientOnly>
        <ServersAgentsHero />
        <template #fallback>
          <div
            class="min-h-[340px] animate-pulse rounded-2xl bg-[#07090c] ring-1 ring-white/5 md:min-h-[440px]"
          />
        </template>
      </ClientOnly>

      <div id="server-modes" class="scroll-mt-4">
        <div
          class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
        >
          <template v-if="!ready">
            <Skeleton
              v-for="i in 3"
              :key="i"
              class="aspect-[4/5] rounded-lg"
            />
          </template>
          <template v-else>
            <ServerModeTile
              v-for="(mode, index) in modes"
              :key="mode.key"
              :mode="mode"
              :navigate="false"
              class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
              :style="{ animationDelay: `${index * 50}ms` }"
              @select="openMode(mode.key)"
            />
            <ServerPublicTile
              :navigate="false"
              class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both cursor-pointer"
              :style="{ animationDelay: `${modes.length * 50}ms` }"
              @click="panel = 'public'"
            />
            <ServerHostTile
              :navigate="false"
              class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both cursor-pointer"
              :style="{ animationDelay: `${(modes.length + 1) * 50}ms` }"
              @click="panel = 'host'"
            />
          </template>
        </div>
      </div>
    </section>

    <section v-else class="space-y-3">
      <Button
        type="button"
        variant="ghost"
        size="sm"
        class="h-8 gap-1.5 px-2 text-white/70 hover:text-white"
        @click="goHome"
      >
        <ArrowLeft class="h-3.5 w-3.5" />
        {{ t("pages.servers.back_to_modes") }}
      </Button>

      <GameServersMode
        v-if="panel === 'mode'"
        embedded
        :model-value="pickedMode"
        @update:model-value="pickedMode = $event"
        @back="goHome"
      />
      <HostServersPage v-else-if="panel === 'host'" embedded />
      <PublicServersPage v-else-if="panel === 'public'" />
    </section>
  </PageTransition>
</template>
