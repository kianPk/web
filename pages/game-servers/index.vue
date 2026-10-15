<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import ServerHostTile from "~/components/servers/ServerHostTile.vue";
import ServersAgentsHero from "~/components/servers/ServersAgentsHero.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";

const { loading, servers, modes } = usePublicServerFleet();

const ready = computed(() => !loading.value || servers.value.length > 0);

const { t } = useI18n();
useHead(() => ({ title: t("pages.servers.title") }));
</script>

<template>
  <PageTransition :delay="0">
    <section class="space-y-6">
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
              class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
              :style="{ animationDelay: `${index * 50}ms` }"
            />
            <ServerHostTile
              class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
              :style="{ animationDelay: `${modes.length * 50}ms` }"
            />
          </template>
        </div>
      </div>
    </section>
  </PageTransition>
</template>
