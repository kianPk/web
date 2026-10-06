import type { VariantProps } from "class-variance-authority"
import type { HTMLAttributes } from "vue"
import { cva } from "class-variance-authority"

export interface SidebarProps {
  side?: "left" | "right"
  variant?: "sidebar" | "floating" | "inset"
  collapsible?: "offcanvas" | "icon" | "none"
  overlay?: boolean
  class?: HTMLAttributes["class"]
}

export { default as Sidebar } from "./Sidebar.vue"
export { default as SidebarContent } from "./SidebarContent.vue"
export { default as SidebarFooter } from "./SidebarFooter.vue"
export { default as SidebarGroup } from "./SidebarGroup.vue"
export { default as SidebarGroupAction } from "./SidebarGroupAction.vue"
export { default as SidebarGroupContent } from "./SidebarGroupContent.vue"
export { default as SidebarGroupLabel } from "./SidebarGroupLabel.vue"
export { default as SidebarHeader } from "./SidebarHeader.vue"
export { default as SidebarInput } from "./SidebarInput.vue"
export { default as SidebarInset } from "./SidebarInset.vue"
export { default as SidebarMenu } from "./SidebarMenu.vue"
export { default as SidebarMenuAction } from "./SidebarMenuAction.vue"
export { default as SidebarMenuBadge } from "./SidebarMenuBadge.vue"
export { default as SidebarMenuButton } from "./SidebarMenuButton.vue"
export { default as SidebarMenuItem } from "./SidebarMenuItem.vue"
export { default as SidebarMenuSkeleton } from "./SidebarMenuSkeleton.vue"
export { default as SidebarMenuSub } from "./SidebarMenuSub.vue"
export { default as SidebarMenuSubButton } from "./SidebarMenuSubButton.vue"
export { default as SidebarMenuSubItem } from "./SidebarMenuSubItem.vue"
export { default as SidebarProvider } from "./SidebarProvider.vue"
export { default as SidebarRail } from "./SidebarRail.vue"
export { default as SidebarSeparator } from "./SidebarSeparator.vue"
export { default as SidebarTrigger } from "./SidebarTrigger.vue"

export { useSidebar } from "./utils"

export const sidebarMenuButtonVariants = cva(
  [
    "peer/menu-button flex w-full items-center gap-2 overflow-hidden rounded-md p-2 text-left text-sm outline-none ring-sidebar-ring",
    "text-sidebar-foreground/80 transition-[width,height,padding,background-color,color,box-shadow] duration-150",
    "hover:bg-[hsl(var(--tac-amber)/0.08)] hover:text-sidebar-foreground",
    "focus-visible:ring-2 active:bg-[hsl(var(--tac-amber)/0.12)] active:text-[hsl(var(--tac-amber))]",
    "disabled:pointer-events-none disabled:opacity-50 group-has-[[data-sidebar=menu-action]]/menu-item:pr-8",
    "aria-disabled:pointer-events-none aria-disabled:opacity-50",
    "data-[active=true]:bg-[hsl(var(--tac-amber)/0.12)] data-[active=true]:font-medium data-[active=true]:text-[hsl(var(--tac-amber))]",
    "data-[active=true]:shadow-[inset_2px_0_0_0_hsl(var(--tac-amber))]",
    "[&.router-link-active]:bg-[hsl(var(--tac-amber)/0.12)] [&.router-link-active]:font-medium [&.router-link-active]:text-[hsl(var(--tac-amber))]",
    "[&.router-link-active]:shadow-[inset_2px_0_0_0_hsl(var(--tac-amber))]",
    "data-[state=open]:hover:bg-[hsl(var(--tac-amber)/0.08)] data-[state=open]:hover:text-sidebar-foreground",
    "group-data-[collapsible=icon]:!size-8 group-data-[collapsible=icon]:!p-2",
    "[&>span:last-child]:truncate [&>svg]:size-4 [&>svg]:shrink-0 [&>svg]:opacity-80",
    "[&.router-link-active>svg]:opacity-100 [&[data-active=true]>svg]:opacity-100",
  ].join(" "),
  {
    variants: {
      variant: {
        default: "",
        outline:
          "bg-background shadow-[0_0_0_1px_hsl(var(--sidebar-border))] hover:bg-[hsl(var(--tac-amber)/0.08)] hover:shadow-[0_0_0_1px_hsl(var(--tac-amber)/0.35)]",
      },
      size: {
        default: "h-8 text-sm",
        sm: "h-7 text-xs",
        lg: "h-12 text-sm group-data-[collapsible=icon]:!p-0",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
)

export type SidebarMenuButtonVariants = VariantProps<typeof sidebarMenuButtonVariants>
