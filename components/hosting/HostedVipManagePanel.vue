<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Crown, RefreshCw, Trash2, UserPlus } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import { toast } from "~/components/ui/toast";
import { hostedApi, hostedErrorMessage } from "~/composables/useHostedServers";

const VIP_DURATIONS = ["1d", "7d", "30d", "90d", "perm"] as const;

type VipRow = {
  steam_id: string;
  name: string | null;
  avatar_url: string | null;
  expires_at: string | null;
};

const props = defineProps<{ hostedId: string }>();

const { t } = useI18n();

const vips = ref<VipRow[]>([]);
const form = ref({
  steam_id: "",
  duration: "30d" as (typeof VIP_DURATIONS)[number],
});
const busy = ref(false);
const loaded = ref(false);

function formatRemaining(expiresAt: string | null): string {
  if (!expiresAt) return String(t("pages.hosting.vip_manage.permanent"));
  const ms = new Date(expiresAt).getTime() - Date.now();
  if (ms <= 0) return "—";
  const days = Math.floor(ms / 86_400_000);
  const hours = Math.floor((ms % 86_400_000) / 3_600_000);
  if (days >= 1) {
    return String(
      t("pages.hosting.vip_manage.remaining_days", { days, hours }),
    );
  }
  const mins = Math.floor((ms % 3_600_000) / 60_000);
  if (hours >= 1) {
    return String(
      t("pages.hosting.vip_manage.remaining_hours", { hours, mins }),
    );
  }
  return String(t("pages.hosting.vip_manage.remaining_mins", { mins }));
}

async function load() {
  try {
    const result = await hostedApi<{ vips: VipRow[] }>(
      `/hosted-servers/${props.hostedId}/vips`,
    );
    vips.value = result.vips || [];
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    loaded.value = true;
  }
}

async function run(
  action: () => Promise<string | null>,
): Promise<void> {
  if (busy.value) return;
  busy.value = true;
  try {
    const title = await action();
    if (title) toast({ title });
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = false;
  }
}

function grantVip() {
  const steamId = form.value.steam_id.trim();
  if (!steamId) return;
  return run(async () => {
    const result = await hostedApi<{ listed: boolean; vips: VipRow[] }>(
      `/hosted-servers/${props.hostedId}/vips/grant`,
      {
        method: "POST",
        body: { steam_id: steamId, duration: form.value.duration },
      },
    );
    vips.value = result.vips || [];
    form.value.steam_id = "";
    return String(
      t(
        result.listed
          ? "pages.hosting.vip_manage.added"
          : "pages.hosting.vip_manage.added_unlisted",
      ),
    );
  });
}

function revokeVip(steamId: string) {
  if (
    !window.confirm(String(t("pages.hosting.vip_manage.remove_confirm")))
  ) {
    return;
  }
  return run(async () => {
    const result = await hostedApi<{ vips: VipRow[] }>(
      `/hosted-servers/${props.hostedId}/vips/revoke`,
      { method: "POST", body: { steam_id: steamId } },
    );
    vips.value = result.vips || [];
    return String(t("pages.hosting.vip_manage.removed"));
  });
}

function syncVips() {
  return run(async () => {
    const result = await hostedApi<{
      imported: number;
      removed: number;
      unregistered: number;
      vips: VipRow[];
    }>(`/hosted-servers/${props.hostedId}/vips/sync`, { method: "POST" });
    vips.value = result.vips || [];
    return String(
      t("pages.hosting.vip_manage.synced", {
        imported: result.imported,
        removed: result.removed,
        unregistered: result.unregistered,
      }),
    );
  });
}

onMounted(load);
</script>

<template>
  <section
    class="space-y-4 rounded-xl border border-border/70 bg-card/50 p-5"
  >
    <div class="space-y-1">
      <h2 class="m-0 flex items-center gap-2 text-base font-semibold">
        <Crown class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
        {{ $t("pages.hosting.vip_manage.title") }}
      </h2>
      <p class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.vip_manage.description") }}
      </p>
    </div>

    <form class="flex flex-col gap-2 sm:flex-row" @submit.prevent="grantVip">
      <Input
        v-model="form.steam_id"
        dir="ltr"
        class="min-w-0 flex-1 font-mono"
        maxlength="120"
        :placeholder="$t('pages.hosting.vip_manage.placeholder')"
      />
      <Select v-model="form.duration">
        <SelectTrigger class="w-full shrink-0 sm:w-32">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem
            v-for="duration in VIP_DURATIONS"
            :key="duration"
            :value="duration"
          >
            {{ $t(`pages.hosting.vip_manage.durations.${duration}`) }}
          </SelectItem>
        </SelectContent>
      </Select>
      <Button
        type="submit"
        size="sm"
        class="shrink-0"
        :disabled="busy || !form.steam_id.trim()"
      >
        <UserPlus class="me-1.5 h-3.5 w-3.5" />
        {{ $t("pages.hosting.vip_manage.add") }}
      </Button>
    </form>

    <div class="flex flex-wrap items-center justify-between gap-2">
      <p class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.vip_manage.hint") }}
      </p>
      <Button size="sm" variant="outline" :disabled="busy" @click="syncVips">
        <RefreshCw class="me-1.5 h-3.5 w-3.5" />
        {{ $t("pages.hosting.vip_manage.sync") }}
      </Button>
    </div>

    <p
      v-if="loaded && !vips.length"
      class="m-0 rounded-md border border-dashed border-border px-3 py-6 text-center text-sm text-muted-foreground"
    >
      {{ $t("pages.hosting.vip_manage.none") }}
    </p>
    <ul v-else class="m-0 list-none space-y-1.5 p-0">
      <li
        v-for="vip in vips"
        :key="vip.steam_id"
        class="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm"
      >
        <img
          v-if="vip.avatar_url"
          :src="vip.avatar_url"
          alt=""
          class="h-7 w-7 rounded-full object-cover"
        />
        <span
          v-else
          class="flex h-7 w-7 items-center justify-center rounded-full bg-[hsl(var(--tac-amber)/0.12)] text-[hsl(var(--tac-amber))]"
        >
          <Crown class="h-3.5 w-3.5" />
        </span>
        <div class="min-w-0 flex-1">
          <div class="truncate font-medium">
            {{ vip.name || vip.steam_id }}
          </div>
          <div class="font-mono text-[0.7rem] text-muted-foreground">
            {{ formatRemaining(vip.expires_at) }}
          </div>
        </div>
        <code class="hidden text-xs text-muted-foreground sm:inline" dir="ltr">
          {{ vip.steam_id }}
        </code>
        <Button
          size="icon"
          variant="ghost"
          class="h-7 w-7"
          :disabled="busy"
          :title="$t('pages.hosting.vip_manage.remove')"
          @click="revokeVip(vip.steam_id)"
        >
          <Trash2 class="h-3.5 w-3.5" />
        </Button>
      </li>
    </ul>
  </section>
</template>
