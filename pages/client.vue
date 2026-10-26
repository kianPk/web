<script setup lang="ts">
import { Download, Monitor, ShieldCheck } from "lucide-vue-next";
import { Button } from "~/components/ui/button";

const { t } = useI18n();

const GITHUB_CLIENT_ZIP =
  "https://github.com/kianPk/web/releases/download/yguard-client-v0.2.0/YGuardClient-0.2.0-win-x64.zip";

const config = useRuntimeConfig();
const downloadUrl = computed(() => {
  const fromEnv = (config.public as { clientDownloadUrl?: string })
    .clientDownloadUrl;
  return (typeof fromEnv === "string" && fromEnv) || GITHUB_CLIENT_ZIP;
});
</script>

<template>
  <div class="mx-auto flex max-w-2xl flex-col gap-8 px-4 py-10">
    <div class="space-y-3">
      <p
        class="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground"
      >
        {{ t("layouts.launcher.client") }}
      </p>
      <h1 class="text-3xl font-bold tracking-tight">
        {{ t("layouts.launcher.download_title") }}
      </h1>
      <p class="text-muted-foreground">
        {{ t("layouts.launcher.download_body") }}
      </p>
    </div>

    <div class="flex flex-wrap gap-3">
      <Button as-child size="lg">
        <a :href="downloadUrl" target="_blank" rel="noopener noreferrer">
          <Download class="mr-2 h-4 w-4" />
          {{ t("layouts.launcher.download_windows") }}
        </a>
      </Button>
      <Button as-child variant="outline" size="lg">
        <NuxtLink to="/ac">
          <ShieldCheck class="mr-2 h-4 w-4" />
          {{ t("layouts.launcher.download_ac_hint") }}
        </NuxtLink>
      </Button>
    </div>

    <ul class="space-y-4 text-sm text-muted-foreground">
      <li class="flex gap-3">
        <Monitor class="mt-0.5 h-4 w-4 shrink-0" />
        <span>{{ t("layouts.launcher.download_step_install") }}</span>
      </li>
      <li class="flex gap-3">
        <Monitor class="mt-0.5 h-4 w-4 shrink-0" />
        <span>{{ t("layouts.launcher.download_step_login") }}</span>
      </li>
      <li class="flex gap-3">
        <Monitor class="mt-0.5 h-4 w-4 shrink-0" />
        <span>{{ t("layouts.launcher.download_step_play") }}</span>
      </li>
    </ul>
  </div>
</template>
