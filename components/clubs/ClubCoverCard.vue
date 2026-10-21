<script setup lang="ts">
import { Users } from "lucide-vue-next";
import ClubAvatar from "~/components/clubs/ClubAvatar.vue";
import { resolveAvatarUrl } from "~/utilities/avatarUrl";

const props = defineProps<{
  name: string;
  shortName?: string | null;
  avatarUrl?: string | null;
  coverUrl?: string | null;
  memberCount?: number;
  joinMode?: string;
  primary?: boolean;
  member?: boolean;
}>();

const { t } = useI18n();
const apiDomain = useRuntimeConfig().public.apiDomain as string;
const resolvedCover = computed(() =>
  resolveAvatarUrl(props.coverUrl, apiDomain),
);
const resolvedAvatar = computed(() =>
  resolveAvatarUrl(props.avatarUrl, apiDomain),
);
</script>

<template>
  <article
    class="group relative overflow-hidden rounded-xl border border-border/70 bg-card/40 transition duration-200 hover:-translate-y-0.5 hover:border-[hsl(var(--tac-amber)/0.45)] hover:bg-card/70"
  >
    <div
      class="relative h-28 w-full overflow-hidden sm:h-32"
      :style="
        resolvedCover
          ? {
              backgroundImage: `linear-gradient(180deg, rgba(8,8,10,0.15) 0%, rgba(8,8,10,0.82) 100%), url(${resolvedCover})`,
              backgroundSize: 'cover',
              backgroundPosition: 'center',
            }
          : undefined
      "
      :class="
        resolvedCover
          ? ''
          : 'bg-[radial-gradient(ellipse_at_top_left,hsl(var(--tac-amber)/0.28),transparent_52%),linear-gradient(180deg,#1c1c20_0%,#0c0c0e_100%)]'
      "
    />
    <div class="relative -mt-8 flex items-end gap-3 px-4 pb-4">
      <ClubAvatar
        :name="name"
        :short-name="shortName"
        :src="resolvedAvatar"
        size="md"
      />
      <div class="min-w-0 flex-1 pb-0.5">
        <div class="flex items-center gap-2">
          <h3 class="truncate font-sans text-base font-semibold leading-tight tracking-tight">
            {{ name }}
          </h3>
          <span
            v-if="shortName"
            class="hidden shrink-0 rounded bg-[hsl(var(--tac-amber)/0.12)] px-1.5 py-0.5 font-mono text-[0.62rem] font-bold uppercase tracking-wider text-[hsl(var(--tac-amber))] sm:inline"
          >
            {{ shortName }}
          </span>
        </div>
        <div class="mt-1 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-muted-foreground">
          <span class="inline-flex items-center gap-1">
            <Users class="h-3 w-3" />
            {{ memberCount ?? 0 }}
            {{ t("pages.clubs.members_label") }}
          </span>
          <span class="text-border">·</span>
          <span>{{ joinMode }}</span>
          <span
            v-if="primary"
            class="rounded-full bg-[hsl(var(--tac-amber)/0.16)] px-1.5 py-px text-[0.65rem] font-medium text-[hsl(var(--tac-amber))]"
          >
            {{ t("pages.clubs.representing") }}
          </span>
          <span
            v-else-if="member"
            class="rounded-full bg-muted px-1.5 py-px text-[0.65rem] text-muted-foreground"
          >
            {{ t("pages.clubs.member_badge") }}
          </span>
        </div>
      </div>
    </div>
  </article>
</template>
