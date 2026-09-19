<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Badge } from "~/components/ui/badge";
import { toast } from "~/components/ui/toast";
import { Shield, Download, Link2 } from "lucide-vue-next";
import { useAuthStore } from "~/stores/AuthStore";

const { t } = useI18n();
const apiDomain = useRuntimeConfig().public.apiDomain as string;
const auth = useAuthStore();

const pairCode = ref<string | null>(null);
const pairExpires = ref<string | null>(null);
const status = ref<{
  required: boolean;
  valid: boolean;
  latest: Record<string, unknown> | null;
} | null>(null);
const loading = ref(false);
const launcherVersion = ref("0.3.6");
const launcherUrl = ref(
  "https://github.com/kianPk/web/releases/download/client-v0.3.6/YGuardAC-0.3.6-client.zip",
);

async function loadLauncher() {
  try {
    const release = await $fetch<{ version?: string; download_url?: string }>(
      `https://${apiDomain}/plugins/ac/launcher`,
      { query: { _: Date.now() } },
    );
    if (release?.version) launcherVersion.value = release.version;
    if (release?.download_url) launcherUrl.value = release.download_url;
  } catch {
    /* keep fallback 0.3.6 */
  }
}

async function loadStatus() {
  if (!auth.me?.steam_id) return;
  try {
    status.value = await $fetch(`https://${apiDomain}/plugins/ac/status`, {
      credentials: "include",
    });
  } catch {
    status.value = null;
  }
}

async function startPair() {
  if (!auth.me?.steam_id) {
    toast({ title: t("ac.login_required"), variant: "destructive" });
    return;
  }
  loading.value = true;
  try {
    const data = await $fetch<{ code: string; expires_at: string }>(
      `https://${apiDomain}/plugins/ac/pair/start`,
      { method: "POST", credentials: "include" },
    );
    pairCode.value = data.code;
    pairExpires.value = data.expires_at;
    toast({ title: t("ac.pair_ready") });
  } catch (e: any) {
    toast({
      title: e?.data?.message || t("ac.pair_failed"),
      variant: "destructive",
    });
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadStatus();
  void loadLauncher();
});
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-8 px-4 py-10">
    <PageTransition :delay="0">
      <div class="space-y-3">
        <div class="flex items-center gap-2 text-[hsl(var(--tac-amber))]">
          <Shield class="h-6 w-6" />
          <h1 class="font-sans text-2xl font-semibold tracking-tight">
            {{ $t("ac.title") }}
          </h1>
        </div>
        <p class="text-sm text-muted-foreground">
          {{ $t("ac.description") }}
        </p>
      </div>
    </PageTransition>

    <PageTransition :delay="80">
      <div class="space-y-3 rounded-lg border border-border bg-muted/20 p-4">
        <div class="flex items-center justify-between gap-2">
          <h2 class="text-sm font-medium">{{ $t("ac.status_title") }}</h2>
          <Badge :variant="status?.valid ? 'default' : 'secondary'">
            {{
              status?.valid
                ? $t("ac.status_valid")
                : $t("ac.status_invalid")
            }}
          </Badge>
        </div>
        <p class="text-xs text-muted-foreground">
          {{
            status?.required
              ? $t("ac.required_on")
              : $t("ac.required_off")
          }}
        </p>
        <Button size="sm" variant="outline" @click="loadStatus">
          {{ $t("ac.refresh") }}
        </Button>
      </div>
    </PageTransition>

    <PageTransition :delay="120">
      <div class="space-y-4 rounded-lg border border-border p-4">
        <h2 class="flex items-center gap-2 text-sm font-medium">
          <Download class="h-4 w-4" />
          {{ $t("ac.download_title") }}
        </h2>
        <p class="text-sm text-muted-foreground">
          {{ $t("ac.download_desc") }}
        </p>
        <Button as-child class="bg-orange-600 hover:bg-orange-500">
          <a :href="launcherUrl">
            <Download class="mr-2 h-4 w-4" />
            {{ $t("ac.install_button") }} (v{{ launcherVersion }})
          </a>
        </Button>
        <ol class="list-decimal space-y-2 pl-5 text-sm text-muted-foreground">
          <li>{{ $t("ac.step_download") }}</li>
          <li>{{ $t("ac.step_pair") }}</li>
          <li>{{ $t("ac.step_checks") }}</li>
          <li>{{ $t("ac.step_queue") }}</li>
        </ol>
      </div>
    </PageTransition>

    <PageTransition :delay="160">
      <div class="space-y-4 rounded-lg border border-border p-4">
        <h2 class="flex items-center gap-2 text-sm font-medium">
          <Link2 class="h-4 w-4" />
          {{ $t("ac.pair_title") }}
        </h2>
        <p class="text-sm text-muted-foreground">
          {{ $t("ac.pair_desc") }}
        </p>
        <Button :disabled="loading" @click="startPair">
          {{ $t("ac.generate_code") }}
        </Button>
        <div
          v-if="pairCode"
          class="rounded-md border border-dashed border-[hsl(var(--tac-amber)/0.5)] bg-black/40 px-4 py-6 text-center"
        >
          <div
            class="font-mono text-3xl font-bold tracking-[0.35em] text-[hsl(var(--tac-amber))]"
          >
            {{ pairCode }}
          </div>
          <p class="mt-2 text-xs text-muted-foreground">
            {{ $t("ac.code_expires") }}
          </p>
        </div>
      </div>
    </PageTransition>
  </div>
</template>
