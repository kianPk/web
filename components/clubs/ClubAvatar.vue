<script setup lang="ts">
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { resolveAvatarUrl } from "~/utilities/avatarUrl";

const props = withDefaults(
  defineProps<{
    name: string;
    shortName?: string | null;
    src?: string | null;
    size?: "sm" | "md" | "lg" | "xl";
    ring?: boolean;
    class?: string;
  }>(),
  { size: "md", ring: true },
);

const apiDomain = useRuntimeConfig().public.apiDomain as string;
const resolvedSrc = computed(() => resolveAvatarUrl(props.src, apiDomain));

const sizeClass = computed(
  () =>
    ({
      sm: "h-10 w-10 text-[0.7rem]",
      md: "h-14 w-14 text-base",
      lg: "h-24 w-24 text-2xl",
      xl: "h-[7.25rem] w-[7.25rem] text-3xl",
    })[props.size],
);

const initials = computed(() =>
  (props.shortName || props.name || "?").replace(/\s+/g, "").slice(0, 2).toUpperCase(),
);
</script>

<template>
  <Avatar
    shape="circle"
    :class="[
      sizeClass,
      'bg-[hsl(var(--tac-amber)/0.14)] font-bold uppercase text-[hsl(var(--tac-amber))]',
      ring
        ? 'border-2 border-[hsl(var(--tac-amber)/0.5)] shadow-[0_0_0_4px_hsl(var(--background))]'
        : 'border border-border',
      props.class,
    ]"
  >
    <AvatarImage v-if="resolvedSrc" :src="resolvedSrc" :alt="name" class="h-full w-full object-cover" />
    <AvatarFallback class="bg-transparent font-bold tracking-wide">
      {{ initials }}
    </AvatarFallback>
  </Avatar>
</template>
