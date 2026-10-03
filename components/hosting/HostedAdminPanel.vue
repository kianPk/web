<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Textarea } from "~/components/ui/textarea";
import { toast } from "~/components/ui/toast";
import {
  formatHostedDate,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedAdminSettings,
  type HostedServer,
} from "~/composables/useHostedServers";

const emit = defineEmits<{ changed: [] }>();
const { t, locale } = useI18n();

const servers = ref<HostedServer[]>([]);
const settings = ref<HostedAdminSettings | null>(null);
const form = ref({
  enabled: true,
  max_active: 3,
  reserve_match_slots: 2,
  grace_days: 3,
  gslt_pool: "",
  steam_api_key: "",
});
const busy = ref(false);

async function load() {
  try {
    const [list, current] = await Promise.all([
      hostedApi<HostedServer[]>("/hosted-servers/admin/list"),
      hostedApi<HostedAdminSettings>("/hosted-servers/admin/settings"),
    ]);
    servers.value = list;
    applySettings(current);
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  }
}

function applySettings(current: HostedAdminSettings) {
  settings.value = current;
  form.value = {
    ...form.value,
    enabled: current.enabled,
    max_active: current.max_active,
    reserve_match_slots: current.reserve_match_slots,
    grace_days: current.grace_days,
    gslt_pool: "",
    steam_api_key: "",
  };
}

async function act(action: () => Promise<unknown>) {
  if (busy.value) return;
  busy.value = true;
  try {
    await action();
    await load();
    emit("changed");
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = false;
  }
}

function saveSettings() {
  return act(async () => {
    const body: Record<string, unknown> = {
      enabled: form.value.enabled,
      max_active: Number(form.value.max_active),
      reserve_match_slots: Number(form.value.reserve_match_slots),
      grace_days: Number(form.value.grace_days),
    };
    if (form.value.gslt_pool.trim()) body.gslt_pool = form.value.gslt_pool;
    if (form.value.steam_api_key.trim()) {
      body.steam_api_key = form.value.steam_api_key;
    }
    applySettings(
      await hostedApi<HostedAdminSettings>("/hosted-servers/admin/settings", {
        method: "POST",
        body,
      }),
    );
    toast({ title: t("pages.hosting.panel.saved") });
  });
}

function extend(server: HostedServer) {
  const value = window.prompt(t("pages.hosting.admin.extend_prompt"), "30");
  if (!value) return;
  return act(() =>
    hostedApi(`/hosted-servers/admin/${server.id}/extend`, {
      method: "POST",
      body: { days: Number(value) },
    }),
  );
}

function suspend(server: HostedServer, suspended: boolean) {
  return act(() =>
    hostedApi(`/hosted-servers/admin/${server.id}/suspend`, {
      method: "POST",
      body: { suspended },
    }),
  );
}

function retry(server: HostedServer) {
  return act(() =>
    hostedApi(`/hosted-servers/admin/${server.id}/retry`, { method: "POST" }),
  );
}

function remove(server: HostedServer) {
  if (!window.confirm(t("pages.hosting.admin.delete_confirm"))) return;
  return act(() =>
    hostedApi(`/hosted-servers/admin/${server.id}/delete`, { method: "POST" }),
  );
}

function setGslt(server: HostedServer) {
  const token = window.prompt(t("pages.hosting.admin.gslt_prompt"), "");
  if (token === null) return;
  return act(() =>
    hostedApi(`/hosted-servers/admin/${server.id}/gslt`, {
      method: "POST",
      body: { token },
    }),
  );
}

onMounted(() => {
  void load();
});
</script>

<template>
  <section class="space-y-4 rounded-lg border border-dashed border-border p-4">
    <h2 class="m-0 text-lg font-semibold">
      {{ $t("pages.hosting.admin.title") }}
    </h2>

    <div v-if="settings" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <button
        type="button"
        class="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-start text-sm"
        @click="form.enabled = !form.enabled"
      >
        <Checkbox
          :model-value="form.enabled"
          class="pointer-events-none"
          tabindex="-1"
        />
        {{ $t("pages.hosting.admin.sales_enabled") }}
      </button>
      <div class="space-y-1">
        <Label>{{ $t("pages.hosting.admin.max_active") }}</Label>
        <Input v-model="form.max_active" type="number" min="0" />
      </div>
      <div class="space-y-1">
        <Label>{{ $t("pages.hosting.admin.reserve_match_slots") }}</Label>
        <Input v-model="form.reserve_match_slots" type="number" min="0" />
      </div>
      <div class="space-y-1">
        <Label>{{ $t("pages.hosting.admin.grace_days") }}</Label>
        <Input v-model="form.grace_days" type="number" min="0" />
      </div>
      <div class="space-y-1 sm:col-span-2">
        <Label>
          {{
            $t("pages.hosting.admin.gslt_pool", {
              n: settings.gslt_pool_size,
            })
          }}
        </Label>
        <Textarea
          v-model="form.gslt_pool"
          rows="2"
          dir="ltr"
          class="font-mono text-xs"
          :placeholder="$t('pages.hosting.admin.gslt_pool_placeholder')"
        />
      </div>
      <div class="space-y-1 sm:col-span-2">
        <Label>
          {{
            settings.steam_api_key_set
              ? $t("pages.hosting.admin.steam_api_key_set")
              : $t("pages.hosting.admin.steam_api_key_unset")
          }}
        </Label>
        <Input
          v-model="form.steam_api_key"
          dir="ltr"
          class="font-mono text-xs"
          :placeholder="$t('pages.hosting.admin.steam_api_key_placeholder')"
        />
      </div>
    </div>
    <Button size="sm" :disabled="busy" @click="saveSettings">
      {{ $t("pages.hosting.panel.save") }}
    </Button>

    <div class="overflow-x-auto">
      <table class="w-full text-sm">
        <thead class="text-xs text-muted-foreground">
          <tr class="border-b border-border">
            <th class="p-2 text-start">
              {{ $t("pages.hosting.server_name") }}
            </th>
            <th class="p-2 text-start">
              {{ $t("pages.hosting.admin.owner") }}
            </th>
            <th class="p-2 text-start">
              {{ $t("pages.hosting.admin.status") }}
            </th>
            <th class="p-2 text-start">
              {{ $t("pages.hosting.panel.expires") }}
            </th>
            <th class="p-2 text-start">GSLT</th>
            <th class="p-2"></th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="server in servers"
            :key="server.id"
            class="border-b border-border/60"
          >
            <td class="p-2">
              <NuxtLink :to="`/hosting/${server.id}`" class="hover:underline">
                {{ server.label }}
              </NuxtLink>
              <div class="text-xs text-muted-foreground">
                {{ server.slots }} · {{ server.type || "—" }}
              </div>
            </td>
            <td class="p-2">
              <NuxtLink
                :to="`/players/${server.owner_steam_id}`"
                class="hover:underline"
              >
                {{ server.owner_name || server.owner_steam_id }}
              </NuxtLink>
            </td>
            <td class="p-2">
              <Badge :variant="hostedStatusVariant(server.status)">
                {{ $t(`pages.hosting.status.${server.status}`) }}
              </Badge>
              <div
                v-if="server.status_detail"
                class="mt-1 max-w-56 text-xs text-muted-foreground"
              >
                {{ server.status_detail }}
              </div>
            </td>
            <td class="p-2 text-xs">
              {{ formatHostedDate(server.expires_at, locale) }}
            </td>
            <td class="p-2 text-xs">
              {{ server.server_id ? (server.has_gslt ? "✓" : "✗") : "—" }}
            </td>
            <td class="p-2">
              <div class="flex flex-wrap justify-end gap-1">
                <Button
                  size="sm"
                  variant="outline"
                  :disabled="busy"
                  @click="extend(server)"
                >
                  {{ $t("pages.hosting.admin.extend") }}
                </Button>
                <Button
                  v-if="server.server_id"
                  size="sm"
                  variant="outline"
                  :disabled="busy"
                  @click="setGslt(server)"
                >
                  GSLT
                </Button>
                <Button
                  v-if="
                    server.status === 'active' || server.status === 'expired'
                  "
                  size="sm"
                  variant="outline"
                  :disabled="busy"
                  @click="suspend(server, true)"
                >
                  {{ $t("pages.hosting.admin.suspend") }}
                </Button>
                <Button
                  v-if="server.status === 'suspended'"
                  size="sm"
                  variant="outline"
                  :disabled="busy"
                  @click="suspend(server, false)"
                >
                  {{ $t("pages.hosting.admin.unsuspend") }}
                </Button>
                <Button
                  v-if="
                    server.status === 'failed' || server.status === 'deleted'
                  "
                  size="sm"
                  variant="outline"
                  :disabled="busy"
                  @click="retry(server)"
                >
                  {{ $t("pages.hosting.admin.retry") }}
                </Button>
                <Button
                  v-if="server.status !== 'deleted'"
                  size="sm"
                  variant="destructive"
                  :disabled="busy"
                  @click="remove(server)"
                >
                  {{ $t("pages.hosting.admin.delete") }}
                </Button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <p
        v-if="!servers.length"
        class="m-0 py-4 text-center text-sm text-muted-foreground"
      >
        {{ $t("pages.hosting.no_servers") }}
      </p>
    </div>
  </section>
</template>
