<script setup lang="ts">
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <SettingsSection
        id="scrim-finder"
        :title="$t('pages.settings.application.scrim_finder.section')"
        :description="
          $t('pages.settings.application.scrim_finder.enabled_description')
        "
        clickable-header
        @header-click="save(!enabled)"
      >
        <template #action>
          <Switch :model-value="enabled" @update:model-value="save" />
        </template>
      </SettingsSection>
    </PageTransition>

    <PageTransition :delay="0.05">
      <SettingsSection
        id="scrim-wagers"
        :title="$t('pages.settings.application.scrim_finder.wagers_section')"
        :description="
          $t('pages.settings.application.scrim_finder.wagers_description')
        "
        clickable-header
        @header-click="saveWagers(!wagersEnabled)"
      >
        <template #action>
          <Switch
            :model-value="wagersEnabled"
            @update:model-value="saveWagers"
          />
        </template>

        <form
          class="flex flex-col gap-3 sm:flex-row sm:items-end"
          @submit.prevent="saveRake"
        >
          <label class="flex-1 space-y-1">
            <span class="text-sm font-medium">
              {{ $t("pages.settings.application.scrim_finder.rake_label") }}
            </span>
            <Input
              v-model="rakeInput"
              type="number"
              min="0"
              max="50"
              step="1"
              class="max-w-[160px]"
            />
            <span class="block text-xs text-muted-foreground">
              {{ $t("pages.settings.application.scrim_finder.rake_help") }}
            </span>
          </label>
          <Button type="submit" :disabled="!rakeValid">
            {{ $t("common.save") }}
          </Button>
        </form>
      </SettingsSection>
    </PageTransition>
  </SettingsPage>
</template>

<script lang="ts">
import { settings_constraint, settings_update_column } from "~/generated/zeus";
import { generateMutation } from "~/graphql/graphqlGen";
import { toast } from "@/components/ui/toast";

const DEFAULT_RAKE = 5;

export default {
  data() {
    return {
      rakeInput: "",
    };
  },
  computed: {
    settings(): { name: string; value: string }[] {
      return useApplicationSettingsStore().settings;
    },
    enabled(): boolean {
      // Enabled by default — only an explicit "false" disables it.
      return (
        this.settings.find((s) => s.name === "public.scrim_finder_enabled")
          ?.value !== "false"
      );
    },
    wagersEnabled(): boolean {
      return (
        this.settings.find((s) => s.name === "public.scrim_wager_enabled")
          ?.value !== "false"
      );
    },
    savedRake(): string {
      return (
        this.settings.find((s) => s.name === "public.scrim_wager_rake_percent")
          ?.value || String(DEFAULT_RAKE)
      );
    },
    rakeValid(): boolean {
      const value = Number(this.rakeInput);
      return (
        String(this.rakeInput) !== "" &&
        Number.isInteger(value) &&
        value >= 0 &&
        value <= 50
      );
    },
  },
  watch: {
    savedRake: {
      immediate: true,
      handler(value: string) {
        this.rakeInput = value;
      },
    },
  },
  methods: {
    async saveSetting(name: string, value: string) {
      await (this.$apollo as any).mutate({
        mutation: generateMutation({
          insert_settings: [
            {
              objects: [{ name, value }],
              on_conflict: {
                constraint: settings_constraint.settings_pkey,
                update_columns: [settings_update_column.value],
              },
            },
            {
              __typename: true,
            },
          ],
        }),
      });

      toast({
        title: this.$t("pages.settings.application.scrim_finder.updated"),
      });
    },
    async save(value: boolean) {
      await this.saveSetting(
        "public.scrim_finder_enabled",
        value ? "true" : "false",
      );
    },
    async saveWagers(value: boolean) {
      await this.saveSetting(
        "public.scrim_wager_enabled",
        value ? "true" : "false",
      );
    },
    async saveRake() {
      if (!this.rakeValid) {
        return;
      }
      await this.saveSetting(
        "public.scrim_wager_rake_percent",
        String(Number(this.rakeInput)),
      );
    },
  },
};
</script>
