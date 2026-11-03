<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  KeyRound,
  Plus,
  RefreshCw,
  Trash2,
  Monitor,
  Copy,
} from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Badge } from "~/components/ui/badge";
import { Spinner } from "~/components/ui/spinner";
import { toast } from "~/components/ui/toast";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "~/components/ui/alert-dialog";

definePageMeta({
  middleware: "admin",
});

const { t } = useI18n();
const apiDomain = useRuntimeConfig().public.apiDomain as string;

type CodeRow = {
  id: string;
  code: string;
  label: string;
  note: string;
  club_id: string | null;
  club_name: string | null;
  max_devices: number;
  created_at: string;
  revoked_at: string | null;
  device_count: number;
  active_device_count: number;
  active_session_count: number;
};

type DeviceRow = {
  id: string;
  code_id: string;
  device_id: string;
  device_label: string;
  bound_by_steam_id: string | null;
  bound_by_name: string | null;
  bound_at: string;
  last_seen_at: string;
  revoked_at: string | null;
};

const loading = ref(false);
const codes = ref<CodeRow[]>([]);
const creating = ref(false);
const label = ref("");
const note = ref("");
const clubId = ref("");
const maxDevices = ref(50);
const customCode = ref("");
const expanded = ref<string | null>(null);
const devices = ref<DeviceRow[]>([]);
const devicesLoading = ref(false);
const revokeCodeTarget = ref<CodeRow | null>(null);
const revokeDeviceTarget = ref<DeviceRow | null>(null);
const revoking = ref(false);

const activeCodes = computed(() =>
  codes.value.filter((c) => !c.revoked_at),
);
const revokedCodes = computed(() =>
  codes.value.filter((c) => !!c.revoked_at),
);

function api(path: string, init?: RequestInit) {
  return $fetch(path.startsWith("http") ? path : `https://${apiDomain}${path}`, {
    ...init,
    credentials: "include",
  });
}

async function loadCodes() {
  loading.value = true;
  try {
    const result = await api<{ codes: CodeRow[] }>(
      "/hosted-servers/gamenet/admin/codes",
    );
    codes.value = result?.codes || [];
  } catch (error: any) {
    toast({
      title: t("pages.settings.application.gamenet.load_failed"),
      description: error?.data?.message || error?.message,
      variant: "destructive",
    });
  } finally {
    loading.value = false;
  }
}

async function createCode() {
  if (creating.value) return;
  creating.value = true;
  try {
    await api("/hosted-servers/gamenet/admin/codes", {
      method: "POST",
      body: {
        label: label.value.trim(),
        note: note.value.trim(),
        club_id: clubId.value.trim() || null,
        max_devices: Number(maxDevices.value) || 50,
        code: customCode.value.trim() || undefined,
      },
    });
    label.value = "";
    note.value = "";
    clubId.value = "";
    customCode.value = "";
    maxDevices.value = 50;
    toast({ title: t("pages.settings.application.gamenet.created") });
    await loadCodes();
  } catch (error: any) {
    toast({
      title: t("pages.settings.application.gamenet.create_failed"),
      description: error?.data?.message || error?.message,
      variant: "destructive",
    });
  } finally {
    creating.value = false;
  }
}

async function toggleDevices(row: CodeRow) {
  if (expanded.value === row.id) {
    expanded.value = null;
    devices.value = [];
    return;
  }
  expanded.value = row.id;
  devicesLoading.value = true;
  try {
    const result = await api<{ devices: DeviceRow[] }>(
      `/hosted-servers/gamenet/admin/codes/${row.id}/devices`,
    );
    devices.value = result?.devices || [];
  } catch (error: any) {
    toast({
      title: t("pages.settings.application.gamenet.devices_failed"),
      description: error?.data?.message || error?.message,
      variant: "destructive",
    });
    expanded.value = null;
  } finally {
    devicesLoading.value = false;
  }
}

async function confirmRevokeCode() {
  const target = revokeCodeTarget.value;
  if (!target || revoking.value) return;
  revoking.value = true;
  try {
    await api(`/hosted-servers/gamenet/admin/codes/${target.id}`, {
      method: "DELETE",
    });
    toast({ title: t("pages.settings.application.gamenet.revoked") });
    revokeCodeTarget.value = null;
    if (expanded.value === target.id) {
      expanded.value = null;
      devices.value = [];
    }
    await loadCodes();
  } catch (error: any) {
    toast({
      title: t("pages.settings.application.gamenet.revoke_failed"),
      description: error?.data?.message || error?.message,
      variant: "destructive",
    });
  } finally {
    revoking.value = false;
  }
}

async function confirmRevokeDevice() {
  const target = revokeDeviceTarget.value;
  if (!target || revoking.value) return;
  revoking.value = true;
  try {
    await api(`/hosted-servers/gamenet/admin/devices/${target.id}`, {
      method: "DELETE",
    });
    toast({ title: t("pages.settings.application.gamenet.device_revoked") });
    revokeDeviceTarget.value = null;
    if (expanded.value) {
      const result = await api<{ devices: DeviceRow[] }>(
        `/hosted-servers/gamenet/admin/codes/${expanded.value}/devices`,
      );
      devices.value = result?.devices || [];
    }
    await loadCodes();
  } catch (error: any) {
    toast({
      title: t("pages.settings.application.gamenet.revoke_failed"),
      description: error?.data?.message || error?.message,
      variant: "destructive",
    });
  } finally {
    revoking.value = false;
  }
}

async function copyCode(code: string) {
  try {
    await navigator.clipboard.writeText(code);
    toast({ title: t("pages.settings.application.gamenet.copied") });
  } catch {
    toast({
      title: t("pages.settings.application.gamenet.copy_failed"),
      variant: "destructive",
    });
  }
}

function fmt(iso: string | null | undefined) {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

onMounted(loadCodes);
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <div class="space-y-6">
        <SettingsSection
          id="gamenet-create"
          :title="t('pages.settings.application.gamenet.create_section')"
          :description="t('pages.settings.application.gamenet.create_section_desc')"
        >
          <div class="grid gap-3 md:grid-cols-2">
            <div class="space-y-1.5">
              <label class="text-xs text-muted-foreground">
                {{ t("pages.settings.application.gamenet.label") }}
              </label>
              <Input
                v-model="label"
                :placeholder="t('pages.settings.application.gamenet.label_ph')"
              />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs text-muted-foreground">
                {{ t("pages.settings.application.gamenet.club_id") }}
              </label>
              <Input
                v-model="clubId"
                :placeholder="t('pages.settings.application.gamenet.club_id_ph')"
              />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs text-muted-foreground">
                {{ t("pages.settings.application.gamenet.max_devices") }}
              </label>
              <Input v-model.number="maxDevices" type="number" min="1" max="500" />
            </div>
            <div class="space-y-1.5">
              <label class="text-xs text-muted-foreground">
                {{ t("pages.settings.application.gamenet.custom_code") }}
              </label>
              <Input
                v-model="customCode"
                class="font-mono uppercase"
                :placeholder="t('pages.settings.application.gamenet.custom_code_ph')"
              />
            </div>
            <div class="space-y-1.5 md:col-span-2">
              <label class="text-xs text-muted-foreground">
                {{ t("pages.settings.application.gamenet.note") }}
              </label>
              <Input
                v-model="note"
                :placeholder="t('pages.settings.application.gamenet.note_ph')"
              />
            </div>
          </div>
          <div class="mt-4 flex flex-wrap gap-2">
            <Button
              :disabled="creating || label.trim().length < 2"
              @click="createCode"
            >
              <Plus class="size-4" />
              {{ t("pages.settings.application.gamenet.create") }}
            </Button>
            <Button variant="outline" :disabled="loading" @click="loadCodes">
              <RefreshCw class="size-4" :class="{ 'animate-spin': loading }" />
              {{ t("pages.settings.application.gamenet.refresh") }}
            </Button>
          </div>
        </SettingsSection>

        <SettingsSection
          id="gamenet-codes"
          :title="t('pages.settings.application.gamenet.list_section')"
          :description="t('pages.settings.application.gamenet.list_section_desc')"
        >
          <div v-if="loading && !codes.length" class="flex justify-center py-8">
            <Spinner />
          </div>
          <div
            v-else-if="!activeCodes.length"
            class="py-6 text-sm text-muted-foreground"
          >
            {{ t("pages.settings.application.gamenet.empty") }}
          </div>
          <div v-else class="space-y-3">
            <div
              v-for="row in activeCodes"
              :key="row.id"
              class="rounded-lg border border-border/70 p-3"
            >
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div class="min-w-0 space-y-1">
                  <div class="flex flex-wrap items-center gap-2">
                    <KeyRound class="size-4 text-muted-foreground" />
                    <button
                      type="button"
                      class="font-mono text-base tracking-wider hover:underline"
                      @click="copyCode(row.code)"
                    >
                      {{ row.code }}
                    </button>
                    <Button
                      size="icon"
                      variant="ghost"
                      class="size-7"
                      @click="copyCode(row.code)"
                    >
                      <Copy class="size-3.5" />
                    </Button>
                    <Badge variant="secondary">2× XP</Badge>
                  </div>
                  <div class="text-sm font-medium">{{ row.label }}</div>
                  <div class="text-xs text-muted-foreground">
                    <span v-if="row.club_name">
                      {{ t("pages.settings.application.gamenet.club") }}:
                      {{ row.club_name }}
                    </span>
                    <span v-else>
                      {{ t("pages.settings.application.gamenet.no_club") }}
                    </span>
                    ·
                    {{ t("pages.settings.application.gamenet.devices_count", {
                      active: row.active_device_count,
                      max: row.max_devices,
                    }) }}
                    ·
                    {{ t("pages.settings.application.gamenet.sessions_count", {
                      count: row.active_session_count,
                    }) }}
                  </div>
                  <div v-if="row.note" class="text-xs text-muted-foreground">
                    {{ row.note }}
                  </div>
                  <div class="text-[11px] text-muted-foreground">
                    {{ t("pages.settings.application.gamenet.created_at") }}:
                    {{ fmt(row.created_at) }}
                  </div>
                </div>
                <div class="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    @click="toggleDevices(row)"
                  >
                    <Monitor class="size-4" />
                    {{
                      expanded === row.id
                        ? t("pages.settings.application.gamenet.hide_devices")
                        : t("pages.settings.application.gamenet.show_devices")
                    }}
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive"
                    @click="revokeCodeTarget = row"
                  >
                    <Trash2 class="size-4" />
                    {{ t("pages.settings.application.gamenet.revoke") }}
                  </Button>
                </div>
              </div>

              <div
                v-if="expanded === row.id"
                class="mt-3 border-t border-border/60 pt-3"
              >
                <div
                  v-if="devicesLoading"
                  class="flex justify-center py-4"
                >
                  <Spinner />
                </div>
                <div
                  v-else-if="!devices.length"
                  class="text-sm text-muted-foreground"
                >
                  {{ t("pages.settings.application.gamenet.no_devices") }}
                </div>
                <div v-else class="space-y-2">
                  <div
                    v-for="dev in devices"
                    :key="dev.id"
                    class="flex flex-wrap items-center justify-between gap-2 rounded-md bg-muted/40 px-3 py-2 text-sm"
                  >
                    <div class="min-w-0">
                      <div class="font-medium">
                        {{
                          dev.device_label ||
                          t("pages.settings.application.gamenet.unnamed_device")
                        }}
                        <Badge
                          v-if="dev.revoked_at"
                          variant="outline"
                          class="ml-2"
                        >
                          {{ t("pages.settings.application.gamenet.revoked_badge") }}
                        </Badge>
                      </div>
                      <div class="truncate font-mono text-[11px] text-muted-foreground">
                        {{ dev.device_id }}
                      </div>
                      <div class="text-[11px] text-muted-foreground">
                        {{ t("pages.settings.application.gamenet.bound_by") }}:
                        {{
                          dev.bound_by_name ||
                          dev.bound_by_steam_id ||
                          "—"
                        }}
                        ·
                        {{ t("pages.settings.application.gamenet.last_seen") }}:
                        {{ fmt(dev.last_seen_at) }}
                      </div>
                    </div>
                    <Button
                      v-if="!dev.revoked_at"
                      size="sm"
                      variant="ghost"
                      @click="revokeDeviceTarget = dev"
                    >
                      <Trash2 class="size-4" />
                      {{ t("pages.settings.application.gamenet.unbind") }}
                    </Button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-if="revokedCodes.length" class="mt-8 space-y-2">
            <div class="text-xs uppercase tracking-wider text-muted-foreground">
              {{ t("pages.settings.application.gamenet.revoked_section") }}
            </div>
            <div
              v-for="row in revokedCodes"
              :key="row.id"
              class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-dashed border-border/60 px-3 py-2 text-sm opacity-70"
            >
              <div>
                <span class="font-mono">{{ row.code }}</span>
                — {{ row.label }}
              </div>
              <span class="text-xs text-muted-foreground">
                {{ fmt(row.revoked_at) }}
              </span>
            </div>
          </div>
        </SettingsSection>
      </div>
    </PageTransition>

    <AlertDialog
      :open="!!revokeCodeTarget"
      @update:open="(v) => !v && (revokeCodeTarget = null)"
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {{ t("pages.settings.application.gamenet.revoke_confirm_title") }}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {{
              t("pages.settings.application.gamenet.revoke_confirm_desc", {
                code: revokeCodeTarget?.code || "",
                label: revokeCodeTarget?.label || "",
              })
            }}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>
            {{ t("pages.settings.application.gamenet.cancel") }}
          </AlertDialogCancel>
          <AlertDialogAction :disabled="revoking" @click="confirmRevokeCode">
            {{ t("pages.settings.application.gamenet.revoke") }}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>

    <AlertDialog
      :open="!!revokeDeviceTarget"
      @update:open="(v) => !v && (revokeDeviceTarget = null)"
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {{ t("pages.settings.application.gamenet.unbind_confirm_title") }}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {{ t("pages.settings.application.gamenet.unbind_confirm_desc") }}
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>
            {{ t("pages.settings.application.gamenet.cancel") }}
          </AlertDialogCancel>
          <AlertDialogAction :disabled="revoking" @click="confirmRevokeDevice">
            {{ t("pages.settings.application.gamenet.unbind") }}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  </SettingsPage>
</template>
