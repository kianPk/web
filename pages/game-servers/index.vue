<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import {
  Crosshair,
  HeartPulse,
  Palette,
  Radio,
  ShieldCheck,
  Swords,
  Timer,
  Trophy,
  Users,
  Zap,
} from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";

const { loading, servers, modes } = usePublicServerFleet();

const ready = computed(() => !loading.value || servers.value.length > 0);

const HIGHLIGHTS = [
  { key: "always_on", icon: Radio, accent: "#12a594" },
  { key: "quick", icon: Zap, accent: "#f76b15" },
  { key: "skins", icon: Palette, accent: "#d6409f" },
  { key: "duels", icon: Swords, accent: "#f5a524", to: "/game-servers/duels" },
  { key: "dm", icon: HeartPulse, accent: "#e5484d", to: "/game-servers/dm" },
  { key: "bhop", icon: Timer, accent: "#46a758", to: "/game-servers/bhop" },
  { key: "awp", icon: Crosshair, accent: "#8e4ec6", to: "/game-servers/awp" },
  { key: "2x2", icon: Users, accent: "#3e8ed0", to: "/game-servers/2x2" },
  { key: "protected", icon: ShieldCheck, accent: "#0090ff" },
  { key: "ranked", icon: Trophy, accent: "#e3d39a", to: "/battle-pass" },
] as const;

const NuxtLink = resolveComponent("NuxtLink");

const { t } = useI18n();
useHead(() => ({ title: t("pages.servers.title") }));
</script>

<template>
  <PageTransition :delay="0">
    <section>
      <div class="flex flex-col gap-1">
        <h1 class="text-2xl font-extrabold tracking-tight md:text-3xl">
          {{ $t("pages.servers.hero_title") }}
        </h1>
        <p class="text-sm text-muted-foreground">
          {{ $t("pages.servers.subtitle") }}
        </p>
      </div>

      <div
        class="mt-5 grid grid-cols-1 gap-2.5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5"
      >
        <component
          :is="'to' in item ? NuxtLink : 'div'"
          v-for="(item, index) in HIGHLIGHTS"
          :key="item.key"
          :to="'to' in item ? item.to : undefined"
          class="group relative overflow-hidden rounded-xl bg-card p-4 ring-1 ring-white/5 transition-all duration-300 animate-in fade-in slide-in-from-bottom-2 fill-mode-both hover:-translate-y-0.5 hover:ring-white/15"
          :style="{ animationDelay: `${index * 40}ms` }"
        >
          <div
            class="pointer-events-none absolute inset-0 opacity-60 transition-opacity duration-300 group-hover:opacity-100"
            :style="{
              backgroundImage: `radial-gradient(130% 120% at 100% 0%, ${item.accent}2e 0%, transparent 55%)`,
            }"
          />
          <div
            class="pointer-events-none absolute inset-x-0 top-0 h-px"
            :style="{
              backgroundImage: `linear-gradient(90deg, transparent, ${item.accent}, transparent)`,
            }"
          />
          <component
            :is="item.icon"
            class="pointer-events-none absolute -bottom-3 -end-3 h-20 w-20 opacity-[0.06] transition-transform duration-500 group-hover:-rotate-6 group-hover:scale-110"
            :style="{ color: item.accent }"
          />

          <div class="relative flex h-full flex-col gap-3">
            <span
              class="inline-flex h-10 w-10 items-center justify-center rounded-lg ring-1"
              :style="{
                backgroundColor: `${item.accent}1f`,
                '--tw-ring-color': `${item.accent}40`,
              }"
            >
              <component
                :is="item.icon"
                class="h-5 w-5"
                :style="{ color: item.accent }"
              />
            </span>
            <div class="space-y-1">
              <p class="text-sm font-bold leading-snug">
                {{ $t(`pages.servers.highlights.${item.key}.title`) }}
              </p>
              <p class="text-xs leading-relaxed text-muted-foreground">
                {{ $t(`pages.servers.highlights.${item.key}.text`) }}
              </p>
            </div>
          </div>
        </component>
      </div>
    </section>
  </PageTransition>

  <PageTransition :delay="80">
    <div
      class="mt-6 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6"
    >
      <template v-if="!ready">
        <Skeleton v-for="i in 3" :key="i" class="aspect-[4/5] rounded-lg" />
      </template>
      <ServerModeTile
        v-for="(mode, index) in modes"
        v-else
        :key="mode.key"
        :mode="mode"
        class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
        :style="{ animationDelay: `${index * 50}ms` }"
      />
    </div>
  </PageTransition>
</template>
