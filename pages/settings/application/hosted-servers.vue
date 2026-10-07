<script setup lang="ts">
import HostedAdminPanel from "~/components/hosting/HostedAdminPanel.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
</script>

<template>
  <SettingsPage width="full">
    <PageTransition :delay="0">
      <div class="space-y-4">
        <div class="space-y-1">
          <h1 class="m-0 text-xl font-semibold">
            {{ $t("pages.hosting.admin.settings_page_title") }}
          </h1>
          <p class="m-0 text-sm text-muted-foreground">
            {{ $t("pages.hosting.admin.settings_page_description") }}
          </p>
        </div>
        <HostedAdminPanel />
      </div>
    </PageTransition>
  </SettingsPage>
</template>
