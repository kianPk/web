<script setup lang="ts">
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import SettingsSaveBar from "~/components/settings/SettingsSaveBar.vue";
import { Switch } from "~/components/ui/switch";

definePageMeta({
  middleware: "admin",
});
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <form @submit.prevent="updateSettings" class="space-y-6">
        <SettingsSection
          id="ypoint-ranked-free"
          :title="$t('pages.settings.application.ypoints.ranked_free_title')"
          :description="
            $t('pages.settings.application.ypoints.ranked_free_description')
          "
          clickable-header
          @header-click="toggleRankedFree"
        >
          <template #action>
            <Switch
              :model-value="rankedFree"
              @update:model-value="toggleRankedFree"
            />
          </template>
        </SettingsSection>

        <SettingsSection
          id="ypoint-costs"
          :title="$t('pages.settings.application.ypoints.costs_title')"
          :description="$t('pages.settings.application.ypoints.costs_description')"
        >
          <p
            v-if="rankedFree"
            class="text-sm text-muted-foreground rounded-md border border-dashed px-3 py-2"
          >
            {{ $t("pages.settings.application.ypoints.ranked_free_active_hint") }}
          </p>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FormField
              v-for="field in costFields"
              :key="field.name"
              v-slot="{ componentField }"
              :name="field.name"
            >
              <FormItem>
                <FormLabel>{{
                  $t(`pages.settings.application.ypoints.fields.${field.label}`)
                }}</FormLabel>
                <FormControl>
                  <Input
                    v-bind="componentField"
                    type="number"
                    min="0"
                    step="1"
                    :disabled="rankedFree && field.ranked"
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            </FormField>
          </div>
        </SettingsSection>

        <SettingsSaveBar
          :form="form"
          :submitting="submitting"
          @save="updateSettings"
        />
      </form>
    </PageTransition>
  </SettingsPage>
</template>

<script lang="ts">
import { settings_constraint, settings_update_column } from "~/generated/zeus";
import { generateMutation } from "~/graphql/graphqlGen";
import { useForm } from "vee-validate";
import { toTypedSchema } from "~/utilities/vee-validate-zod";
import { z } from "zod";
import { toast } from "@/components/ui/toast";
import { useApplicationSettingsStore } from "~/stores/ApplicationSettings";

const COST_FIELDS = [
  {
    key: "ypoint_cost_duel",
    label: "duel",
    fallback: 8,
    ranked: true,
  },
  {
    key: "ypoint_cost_wingman",
    label: "wingman",
    fallback: 0,
    ranked: true,
  },
  {
    key: "ypoint_cost_trios",
    label: "trios",
    fallback: 12,
    ranked: true,
  },
  {
    key: "ypoint_cost_draft_create",
    label: "draft_create",
    fallback: 15,
    ranked: false,
  },
  {
    key: "ypoint_cost_draft_join",
    label: "draft_join",
    fallback: 10,
    ranked: false,
  },
] as const;

const settingName = (key: string) => `public.${key}`;

export default {
  data() {
    return {
      costFields: COST_FIELDS.map((field) => ({
        ...field,
        name: settingName(field.key),
      })),
      submitting: false,
      form: useForm({
        validationSchema: toTypedSchema(
          z.object({
            public: z.object(
              Object.fromEntries(
                COST_FIELDS.map(({ key, fallback }) => [
                  key,
                  z.number().int().min(0).default(fallback),
                ]),
              ),
            ),
          }),
        ),
        initialValues: {
          public: Object.fromEntries(
            COST_FIELDS.map(({ key, fallback }) => [key, fallback]),
          ),
        },
      }),
    };
  },
  watch: {
    settings: {
      immediate: true,
      handler(newVal: Array<{ name: string; value: string | null }>) {
        for (const setting of newVal) {
          if (!COST_FIELDS.some(({ key }) => settingName(key) === setting.name)) {
            continue;
          }
          const parsed = Number(setting.value);
          if (!Number.isNaN(parsed)) {
            (this.form.setFieldValue as any)(setting.name, parsed);
          }
        }
        this.form.resetForm({ values: this.form.values });
      },
    },
  },
  methods: {
    async toggleRankedFree() {
      await (this as any).$apollo.mutate({
        mutation: generateMutation({
          insert_settings_one: [
            {
              object: {
                name: "public.ypoint_ranked_free",
                value: this.rankedFree ? "false" : "true",
              },
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
        title: this.$t("pages.settings.application.ypoints.updated"),
      });
    },
    async updateSettings() {
      if (this.submitting) {
        return;
      }
      this.submitting = true;
      try {
        const values =
          ((this.form.values as any).public as Record<string, number>) ?? {};

        await (this as any).$apollo.mutate({
          mutation: generateMutation({
            insert_settings: [
              {
                objects: COST_FIELDS.map(({ key, fallback }) => ({
                  name: settingName(key),
                  value: String(values[key] ?? fallback),
                })),
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
          title: this.$t("pages.settings.application.ypoints.updated"),
        });
      } finally {
        this.submitting = false;
      }
    },
  },
  computed: {
    settings() {
      return useApplicationSettingsStore().settings;
    },
    rankedFree() {
      const setting = this.settings.find(
        (s: { name: string; value: string | null }) =>
          s.name === "public.ypoint_ranked_free",
      );
      return setting?.value === "true" || setting?.value === "1";
    },
  },
};
</script>
