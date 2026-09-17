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
          id="ypoint-costs"
          :title="$t('pages.settings.application.ypoints.costs_title')"
          :description="$t('pages.settings.application.ypoints.costs_description')"
        >
          <div class="space-y-3">
            <div
              v-for="field in costFields"
              :key="field.name"
              class="flex flex-col gap-3 rounded-lg border p-3 sm:flex-row sm:items-end sm:justify-between"
            >
              <FormField v-slot="{ componentField }" :name="field.name">
                <FormItem class="flex-1 min-w-0">
                  <FormLabel>{{
                    $t(`pages.settings.application.ypoints.fields.${field.label}`)
                  }}</FormLabel>
                  <FormControl>
                    <Input
                      v-bind="componentField"
                      type="number"
                      min="0"
                      step="1"
                      :disabled="field.ranked && isModeFree(field.freeKey)"
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              </FormField>

              <div
                v-if="field.ranked && field.freeKey"
                class="flex items-center justify-between gap-3 rounded-md border px-3 py-2 sm:min-w-[10rem]"
              >
                <span class="text-sm font-medium">{{
                  $t("pages.settings.application.ypoints.free_label")
                }}</span>
                <Switch
                  :model-value="isModeFree(field.freeKey)"
                  @update:model-value="toggleModeFree(field.freeKey)"
                />
              </div>
            </div>
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
    freeKey: "ypoint_free_duel",
  },
  {
    key: "ypoint_cost_wingman",
    label: "wingman",
    fallback: 0,
    ranked: true,
    freeKey: "ypoint_free_wingman",
  },
  {
    key: "ypoint_cost_trios",
    label: "trios",
    fallback: 12,
    ranked: true,
    freeKey: "ypoint_free_trios",
  },
  {
    key: "ypoint_cost_draft_create",
    label: "draft_create",
    fallback: 15,
    ranked: false,
    freeKey: null as string | null,
  },
  {
    key: "ypoint_cost_draft_join",
    label: "draft_join",
    fallback: 10,
    ranked: false,
    freeKey: null as string | null,
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
    isModeFree(freeKey: string | null | undefined) {
      if (!freeKey) return false;
      const setting = this.settings.find(
        (s: { name: string; value: string | null }) =>
          s.name === settingName(freeKey),
      );
      return setting?.value === "true" || setting?.value === "1";
    },
    async toggleModeFree(freeKey: string | null | undefined) {
      if (!freeKey) return;
      const next = this.isModeFree(freeKey) ? "false" : "true";
      await (this as any).$apollo.mutate({
        mutation: generateMutation({
          insert_settings_one: [
            {
              object: {
                name: settingName(freeKey),
                value: next,
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
  },
};
</script>
