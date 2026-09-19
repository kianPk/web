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

const FALLBACK_VERSION = "0.3.5";
const FALLBACK_HREF =
  "https://github.com/kianPk/web/releases/download/client-v0.3.5/YGuardAC-0.3.5-client.zip";

const downloadHref = ref(FALLBACK_HREF);
const advertisedVersion = ref(FALLBACK_VERSION);

function versionParts(v: string): number[] {
  return v
    .trim()
    .split(/[^\d]+/)
    .filter(Boolean)
    .map((n) => Number(n) || 0);
}

function isNewerOrEqual(a: string, b: string): boolean {
  const ap = versionParts(a);
  const bp = versionParts(b);
  const len = Math.max(ap.length, bp.length);
  for (let i = 0; i < len; i++) {
    const x = ap[i] ?? 0;
    const y = bp[i] ?? 0;
    if (x > y) return true;
    if (x < y) return false;
  }
  return true;
}

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
    const apiVersion = release?.version?.trim() || "";
    const apiUrl = release?.download_url?.trim() || "";
    // Don't let a stale API response downgrade the button below the
    // embedded fallback (happens when API deploy lags behind web).
    if (apiVersion && isNewerOrEqual(apiVersion, FALLBACK_VERSION)) {
      advertisedVersion.value = apiVersion;
      if (apiUrl) downloadHref.value = apiUrl;
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
