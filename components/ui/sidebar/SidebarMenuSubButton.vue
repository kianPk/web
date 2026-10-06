<script setup lang="ts">
import type { PrimitiveProps } from "reka-ui"
import type { HTMLAttributes } from "vue"
import { Primitive } from "reka-ui"
import { cn } from "@/lib/utils"

const props = withDefaults(defineProps<PrimitiveProps & {
  size?: "sm" | "md"
  isActive?: boolean
  class?: HTMLAttributes["class"]
}>(), {
  as: "a",
  size: "md",
})
</script>

<template>
  <Primitive
    data-sidebar="menu-sub-button"
    :as="as"
    :as-child="asChild"
    :data-size="size"
    :data-active="isActive"
    :class="cn(
      'flex h-7 min-w-0 -translate-x-px items-center gap-2 overflow-hidden rounded-md px-2 text-sidebar-foreground/75 outline-none ring-sidebar-ring transition-colors hover:bg-[hsl(var(--tac-amber)/0.08)] hover:text-sidebar-foreground focus-visible:ring-2 active:bg-[hsl(var(--tac-amber)/0.12)] active:text-[hsl(var(--tac-amber))] disabled:pointer-events-none disabled:opacity-50 aria-disabled:pointer-events-none aria-disabled:opacity-50 [&>span:last-child]:truncate [&>svg]:size-4 [&>svg]:shrink-0',
      'data-[active=true]:bg-[hsl(var(--tac-amber)/0.12)] data-[active=true]:text-[hsl(var(--tac-amber))]',
      '[&.router-link-active]:bg-[hsl(var(--tac-amber)/0.12)] [&.router-link-active]:text-[hsl(var(--tac-amber))]',
      size === 'sm' && 'text-xs',
      size === 'md' && 'text-sm',
      'group-data-[collapsible=icon]:hidden',
      props.class,
    )"
  >
    <slot />
  </Primitive>
</template>
