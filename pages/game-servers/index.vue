<script setup lang="ts">
import { computed } from "vue";
import { Gamepad2, Radio, Settings2, Users } from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import Empty from "~/components/ui/empty/Empty.vue";
import EmptyTitle from "~/components/ui/empty/EmptyTitle.vue";
import EmptyDescription from "~/components/ui/empty/EmptyDescription.vue";
import Skeleton from "~/components/ui/skeleton/Skeleton.vue";
import ServerModeTile from "~/components/servers/ServerModeTile.vue";
import { usePublicServerFleet } from "~/composables/usePublicServerFleet";
import { useAuthStore } from "~/stores/AuthStore";
import { e_player_roles_enum } from "~/generated/zeus";

const { loading, servers, modes, totalPlayers } = usePublicServerFleet();

const canManage = computed(() =>
  useAuthStore().isRoleAbove(e_player_roles_enum.moderator),
);

const ready = computed(() => !loading.value || servers.value.length > 0);
</script>

<template>
  <PageTransition :delay="0">
    <TacticalPageHeader inline-actions>
      <template #description>{{ $t("pages.servers.eyebrow") }}</template>
      <template #title>{{ $t("pages.servers.title") }}</template>
      <template #subtitle>{{ $t("pages.servers.subtitle") }}</template>
      <template v-if="canManage" #actions>
        <Button as-child variant="outline">
          <NuxtLink to="/dedicated-servers/create">
            <Settings2 class="h-4 w-4" />
            <span class="hidden md:inline">{{
              $t("pages.public_servers.setup_public_server")
            }}</span>
          </NuxtLink>
        </Button>
      </template>
    </TacticalPageHeader>
  </PageTransition>

  <PageTransition :delay="60">
    <div class="mt-5 grid grid-cols-3 gap-3">
      <div
        class="rounded-lg border border-border/70 bg-card/50 px-4 py-3 backdrop-blur-sm"
      >
        <p
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground"
        >
          {{ $t("pages.servers.stat_players") }}
        </p>
        <p
          class="mt-1 flex items-center gap-2 font-mono text-2xl font-bold tabular-nums text-emerald-400"
        >
          <span class="relative flex h-2 w-2">
            <span
              class="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-50"
            />
            <span
              class="relative inline-flex h-2 w-2 rounded-full bg-emerald-400"
            />
          </span>
          {{ totalPlayers }}
        </p>
      </div>
      <div
        class="rounded-lg border border-border/70 bg-card/50 px-4 py-3 backdrop-blur-sm"
      >
        <p
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground"
        >
          {{ $t("pages.servers.stat_servers") }}
        </p>
        <p
          class="mt-1 flex items-center gap-2 font-mono text-2xl font-bold tabular-nums"
        >
          <Radio class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
          {{ servers.length }}
        </p>
      </div>
      <div
        class="rounded-lg border border-border/70 bg-card/50 px-4 py-3 backdrop-blur-sm"
      >
        <p
          class="font-mono text-[0.6rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground"
        >
          {{ $t("pages.servers.stat_modes") }}
        </p>
        <p
          class="mt-1 flex items-center gap-2 font-mono text-2xl font-bold tabular-nums"
        >
          <Gamepad2 class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
          {{ modes.length }}
        </p>
      </div>
    </div>
  </PageTransition>

  <PageTransition :delay="100">
    <div class="mt-6">
      <div
        v-if="!ready"
        class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
      >
        <Skeleton v-for="i in 8" :key="i" class="aspect-[4/3] rounded-xl" />
      </div>

      <Empty v-else-if="modes.length === 0" class="min-h-[220px]">
        <EmptyTitle>{{ $t("pages.servers.empty_title") }}</EmptyTitle>
        <EmptyDescription>{{
          canManage
            ? $t("pages.servers.empty_admin")
            : $t("pages.servers.empty")
        }}</EmptyDescription>
      </Empty>

      <div
        v-else
        class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
      >
        <ServerModeTile
          v-for="(mode, index) in modes"
          :key="mode.key"
          :mode="mode"
          class="animate-in fade-in slide-in-from-bottom-2 fill-mode-both"
          :style="{ animationDelay: `${Math.min(index, 10) * 40}ms` }"
        />
      </div>

      <div
        v-if="ready && modes.length > 0"
        class="mt-6 flex items-center justify-center gap-2 text-xs text-muted-foreground"
      >
        <Users class="h-3.5 w-3.5" />
        <NuxtLink
          to="/public-servers"
          class="underline-offset-4 hover:text-foreground hover:underline"
        >
          {{ $t("pages.servers.all_servers_link") }}
        </NuxtLink>
      </div>
    </div>
  </PageTransition>
</template>
