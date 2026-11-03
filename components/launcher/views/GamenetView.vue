<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { KeyRound, CheckCircle2, Loader2 } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { toast } from "~/components/ui/toast";
import { useAuthStore } from "~/stores/AuthStore";
import { useGamenetDevice } from "~/composables/useGamenetDevice";

const { t } = useI18n();
const auth = useAuthStore();
const apiDomain = useRuntimeConfig().public.apiDomain as string;
const { ensureDeviceId, deviceLabel } = useGamenetDevice();

const code = ref("");
const submitting = ref(false);
const checking = ref(true);
const bound = ref(false);
const active = ref(false);
const label = ref("");
const clubName = ref<string | null>(null);
const expiresAt = ref<string | null>(null);
let heartbeatTimer: ReturnType<typeof setInterval> | null = null;

async function api<T>(path: string, init?: RequestInit) {
  return $fetch<T>(`https://${apiDomain}${path}`, {
    ...init,
    credentials: "include",
  });
}

async function refreshStatus() {
  checking.value = true;
  try {
    const deviceId = ensureDeviceId();
    if (!auth.me?.steam_id) {
      const st = await api<{
        bound: boolean;
        label?: string;
        club_name?: string | null;
      }>(`/hosted-servers/gamenet/status?device_id=${encodeURIComponent(deviceId)}`);
      bound.value = !!st.bound;
      active.value = false;
      label.value = st.label || "";
      clubName.value = st.club_name || null;
      expiresAt.value = null;
      return;
    }
    const hb = await api<{
      bound: boolean;
      active: boolean;
      label?: string;
      club_name?: string | null;
      expires_at?: string;
    }>("/hosted-servers/gamenet/heartbeat", {
      method: "POST",
      body: { device_id: deviceId },
    });
    bound.value = !!hb.bound;
    active.value = !!hb.active;
    label.value = hb.label || "";
    clubName.value = hb.club_name || null;
    expiresAt.value = hb.expires_at || null;
  } catch {
    /* keep prior UI */
  } finally {
    checking.value = false;
  }
}

async function redeem() {
  if (!auth.me?.steam_id) {
    toast({
      title: t("layouts.launcher.gamenet.login_required"),
      variant: "destructive",
    });
    return;
  }
  if (submitting.value) return;
  submitting.value = true;
  try {
    const result = await api<{
      success: boolean;
      label?: string;
      club_name?: string | null;
      session?: { expires_at?: string };
    }>("/hosted-servers/gamenet/redeem", {
      method: "POST",
      body: {
        code: code.value.trim(),
        device_id: ensureDeviceId(),
        device_label: deviceLabel(),
      },
    });
    code.value = "";
    bound.value = true;
    active.value = true;
    label.value = result.label || "";
    clubName.value = result.club_name || null;
    expiresAt.value = result.session?.expires_at || null;
    toast({ title: t("layouts.launcher.gamenet.bound_ok") });
  } catch (error: any) {
    toast({
      title: t("layouts.launcher.gamenet.bound_failed"),
      description: error?.data?.message || error?.message,
      variant: "destructive",
    });
  } finally {
    submitting.value = false;
  }
}

onMounted(async () => {
  ensureDeviceId();
  await refreshStatus();
  heartbeatTimer = setInterval(() => {
    if (auth.me?.steam_id && bound.value) void refreshStatus();
  }, 5 * 60 * 1000);
});

onUnmounted(() => {
  if (heartbeatTimer) clearInterval(heartbeatTimer);
});
</script>

<template>
  <div class="mx-auto flex w-full max-w-xl flex-col gap-6 p-4 md:p-6">
    <div>
      <h1 class="text-xl font-semibold tracking-tight">
        {{ t("layouts.launcher.gamenet.title") }}
      </h1>
      <p class="mt-1 text-sm text-muted-foreground">
        {{ t("layouts.launcher.gamenet.description") }}
      </p>
    </div>

    <div
      v-if="checking"
      class="flex items-center gap-2 text-sm text-muted-foreground"
    >
      <Loader2 class="size-4 animate-spin" />
      {{ t("layouts.launcher.gamenet.checking") }}
    </div>

    <div
      v-else-if="bound"
      class="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4"
    >
      <div class="flex items-start gap-3">
        <CheckCircle2 class="mt-0.5 size-5 text-emerald-400" />
        <div class="space-y-1">
          <div class="font-medium">
            {{ t("layouts.launcher.gamenet.bound_title") }}
          </div>
          <div class="text-sm">
            {{ label }}
            <span v-if="clubName" class="text-muted-foreground">
              · {{ clubName }}
            </span>
          </div>
          <div class="text-xs text-muted-foreground">
            <template v-if="active">
              {{ t("layouts.launcher.gamenet.active_bonus") }}
              <span v-if="expiresAt">
                · {{ t("layouts.launcher.gamenet.until") }}
                {{ new Date(expiresAt).toLocaleString() }}
              </span>
            </template>
            <template v-else>
              {{ t("layouts.launcher.gamenet.login_for_session") }}
            </template>
          </div>
        </div>
      </div>
    </div>

    <div v-else class="space-y-3 rounded-xl border border-border/70 p-4">
      <div class="flex items-center gap-2 text-sm font-medium">
        <KeyRound class="size-4" />
        {{ t("layouts.launcher.gamenet.enter_code") }}
      </div>
      <Input
        v-model="code"
        class="font-mono uppercase tracking-widest"
        maxlength="16"
        :placeholder="t('layouts.launcher.gamenet.code_ph')"
        @keyup.enter="redeem"
      />
      <Button
        class="w-full"
        :disabled="submitting || code.trim().length < 6"
        @click="redeem"
      >
        <Loader2 v-if="submitting" class="size-4 animate-spin" />
        {{ t("layouts.launcher.gamenet.activate") }}
      </Button>
      <p class="text-xs text-muted-foreground">
        {{ t("layouts.launcher.gamenet.hint") }}
      </p>
    </div>
  </div>
</template>
