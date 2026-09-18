<script setup lang="ts">
import { Shield } from "lucide-vue-next";
import { useSidebar } from "@/components/ui/sidebar";
import FiveStackToolTip from "~/components/FiveStackToolTip.vue";

withDefaults(
  defineProps<{
    isMenuItem?: boolean;
  }>(),
  {
    isMenuItem: true,
  },
);

const { state, isMobile } = useSidebar();

const downloadHref =
  "https://github.com/kianPk/web/releases/download/client-v0.3.1/YGuardAC-0.3.1-client.zip";

function download() {
  // Direct navigation so the browser treats it as a file download.
  window.location.href = downloadHref;
}
</script>

<template>
  <div>
    <template v-if="isMenuItem">
      <SidebarMenuItem
        class="mb-1"
        :class="{ 'mx-4': isMobile || state === 'expanded' }"
      >
        <SidebarMenuButton as-child :tooltip="$t('ac.install_tooltip')">
          <Button
            size="sm"
            class="bg-orange-600 text-white hover:bg-orange-500 hover:text-white border-0"
            @click="download"
          >
            <Shield />
            <span v-if="isMobile || state === 'expanded'">{{
              $t("ac.install_button")
            }}</span>
          </Button>
        </SidebarMenuButton>
      </SidebarMenuItem>
    </template>
    <template v-else>
      <FiveStackToolTip v-if="!isMobile">
        <template #trigger>
          <Button
            size="sm"
            class="bg-orange-600 text-white hover:bg-orange-500 hover:text-white border-0"
            @click="download"
          >
            <Shield />
          </Button>
        </template>
        {{ $t("ac.install_button") }}
      </FiveStackToolTip>
    </template>
  </div>
</template>
