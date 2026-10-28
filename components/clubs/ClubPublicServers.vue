<script setup lang="ts">
import { computed, ref } from "vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import { Button } from "~/components/ui/button";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";
import GameServersMode from "~/pages/game-servers/[mode].vue";

const props = defineProps<{
  clubId: string;
}>();

const picked = ref("");
const { loading, servers, modes } = usePublicServerFleet(props.clubId);
const ready = computed(() => !loading.value || servers.value.length > 0);
</script>

<template>
  <PageTransition :delay="0">
    <section v-if="!picked" class="space-y-6">
      <div id="server-modes" class="scroll-mt-4">
        <div
          class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
        >
          <template v-if="!ready">
            <Skeleton v-for="i in 5" :key="i" class="aspect-[4/5] rounded-lg" />
          </template>
          <template v-else>
            <ServerModeTile
              v-for="(mode, index) in modes"
              :key="mode.key"
              :mode="mode"
              :navigate="false"
              class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
              :style="{ animationDelay: `${index * 50}ms` }"
              @select="picked = mode.key"
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
        class="h-8 px-2"
        @click="picked = ''"
      >
        {{ $t("pages.servers.back_to_modes") }}
      </Button>
      <GameServersMode
        embedded
        :club-id="clubId"
        :model-value="picked"
        @update:model-value="picked = $event"
        @back="picked = ''"
      />
    </section>
  </PageTransition>
</template>
