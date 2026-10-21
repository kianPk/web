<script setup lang="ts">
import { Switch } from "~/components/ui/switch";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import SettingsSaveBar from "~/components/settings/SettingsSaveBar.vue";
import {
  settings_constraint,
  settings_update_column,
} from "~/generated/zeus";
import { generateMutation } from "~/graphql/graphqlGen";
import { toast } from "@/components/ui/toast";
import { useApolloClient } from "@vue/apollo-composable";
import {
  SIDEBAR_ADMIN_ONLY_SETTING,
  SIDEBAR_NAV_ITEMS,
} from "~/utilities/sidebarNavVisibility";

definePageMeta({
  middleware: "admin",
});

const { t } = useI18n();
const { client: apollo } = useApolloClient();
const settingsStore = useApplicationSettingsStore();
const submitting = ref(false);
const draft = ref<string[]>([]);

watch(
  () => settingsStore.sidebarAdminOnly,
  (keys) => {
    draft.value = [...keys];
  },
  { immediate: true },
);

const isDirty = computed(() => {
  const a = [...draft.value].sort();
  const b = [...settingsStore.sidebarAdminOnly].sort();
  return JSON.stringify(a) !== JSON.stringify(b);
});

function isAdminOnly(key: string) {
  return draft.value.includes(key);
}

function setAdminOnly(key: string, value: boolean) {
  if (value) {
    if (!draft.value.includes(key)) draft.value = [...draft.value, key];
    return;
  }
  draft.value = draft.value.filter((item) => item !== key);
}

async function save() {
  if (submitting.value) return;
  submitting.value = true;
  try {
    await apollo.mutate({
      mutation: generateMutation({
        insert_settings_one: [
          {
            object: {
              name: SIDEBAR_ADMIN_ONLY_SETTING,
              value: JSON.stringify(draft.value),
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
      title: t("pages.settings.application.update_success"),
    });
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <div class="space-y-6">
        <SettingsSection
          id="sidebar"
          :title="t('pages.settings.application.sidebar.section')"
          :description="t('pages.settings.application.sidebar.description')"
        >
          <div class="divide-y divide-border">
            <div
              class="flex items-center justify-between gap-4 pb-2 text-xs uppercase tracking-wider text-muted-foreground"
            >
              <span>{{ t("pages.settings.application.sidebar.item") }}</span>
              <span>{{ t("pages.settings.application.sidebar.admin_only") }}</span>
            </div>
            <label
              v-for="item in SIDEBAR_NAV_ITEMS"
              :key="item.key"
              class="flex items-center justify-between gap-4 py-3"
            >
              <span class="text-sm">{{ t(item.labelKey) }}</span>
              <Switch
                :model-value="isAdminOnly(item.key)"
                @update:model-value="(v) => setAdminOnly(item.key, v)"
              />
            </label>
          </div>
        </SettingsSection>

        <SettingsSaveBar
          :dirty="isDirty"
          :submitting="submitting"
          @save="save"
          @discard="draft = [...settingsStore.sidebarAdminOnly]"
        />
      </div>
    </PageTransition>
  </SettingsPage>
</template>
