<script setup lang="ts">
import { onMounted, ref } from "vue";
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

const FALLBACK_HREF =
  "https://github.com/kianPk/web/releases/download/client-v0.3.2/YGuardAC-0.3.2-client.zip";

const downloadHref = ref(FALLBACK_HREF);
const advertisedVersion = ref("0.3.2");

onMounted(async () => {
  try {
    const apiDomain = useRuntimeConfig().public.apiDomain as string;
    if (!apiDomain) return;
    const release = await $fetch<{
      version?: string;
      download_url?: string;
    }>(`https://${apiDomain}/plugins/ac/launcher`, {
      // Bust CDN / browser cache — this endpoint used to stick on 0.2.6.
      query: { _: Date.now() },
    });
    if (release?.download_url?.trim()) {
      downloadHref.value = release.download_url.trim();
    }
    if (release?.version?.trim()) {
      advertisedVersion.value = release.version.trim();
    }
  } catch {
    /* keep fallback */
  }
});

function download() {
  window.location.href = downloadHref.value;
}
</script>

<template>
  <div>
    <template v-if="isMenuItem">
      <SidebarMenuItem
        class="mb-1"
        :class="{ 'mx-4': isMobile || state === 'expanded' }"
      >
        <SidebarMenuButton
          as-child
          :tooltip="`${$t('ac.install_tooltip')} (v${advertisedVersion})`"
        >
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
        {{ $t("ac.install_button") }} (v{{ advertisedVersion }})
      </FiveStackToolTip>
    </template>
  </div>
</template>
