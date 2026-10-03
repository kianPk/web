<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import {
  Copy,
  Power,
  RefreshCw,
  RotateCcw,
  Send,
  Terminal,
} from "lucide-vue-next";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Skeleton } from "~/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import {
  Dialog,
  DialogScrollContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "~/components/ui/dialog";
import { toast } from "~/components/ui/toast";
import { formatTomanAmount } from "~/utilities/irrToman";
import {
  formatHostedDate,
  formatHostedDuration,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import {
  hostedApi,
  hostedConnectCommand,
  hostedErrorMessage,
  type HostedOverview,
  type HostedServer,
} from "~/composables/useHostedServers";

const MAPS = [
  "de_dust2",
  "de_mirage",
  "de_inferno",
  "de_nuke",
  "de_ancient",
  "de_anubis",
  "de_overpass",
  "de_train",
  "de_vertigo",
  "cs_office",
  "cs_italy",
];

const { t, locale } = useI18n();
const route = useRoute();
const hostedId = computed(() => String(route.params.id));

const server = ref<HostedServer | null>(null);
const overview = ref<HostedOverview | null>(null);
const loading = ref(true);
const busy = ref<string | null>(null);

const form = ref({ label: "", connect_password: "", type: "Casual" });

const rconCommand = ref("");
const rconLog = ref<Array<{ command: string; result: string }>>([]);
const mapToLoad = ref("de_mirage");

const renewOpen = ref(false);
const renewPlanId = ref("");
const renewTerms = ref(false);

const isActive = computed(() => server.value?.status === "active");
const connectCommand = computed(() =>
  server.value ? hostedConnectCommand(server.value) : null,
);
const renewPlans = computed(() =>
  (overview.value?.plans || []).filter(
    (plan) => plan.hosted_slots === server.value?.slots,
  ),
);
const canRenew = computed(
  () =>
    !!server.value &&
    ["active", "expired", "deleted"].includes(server.value.status),
);

function syncForm() {
  if (!server.value) return;
  form.value = {
    label: server.value.label || "",
    connect_password: server.value.connect_password || "",
    type: server.value.type || "Casual",
  };
}

async function load(silent = false) {
  if (!silent) loading.value = true;
  try {
    server.value = await hostedApi<HostedServer>(
      `/hosted-servers/${hostedId.value}`,
    );
    if (!silent) syncForm();
  } catch (error) {
    if (!silent) {
      server.value = null;
      toast({ variant: "destructive", title: hostedErrorMessage(error) });
    }
  } finally {
    loading.value = false;
  }
}

async function run(
  key: string,
  action: () => Promise<unknown>,
  successTitle?: string,
) {
  if (busy.value) return;
  busy.value = key;
  try {
    await action();
    if (successTitle) {
      toast({ title: successTitle });
    }
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = null;
  }
}

function restart() {
  return run(
    "restart",
    () =>
      hostedApi(`/hosted-servers/${hostedId.value}/restart`, {
        method: "POST",
      }),
    t("pages.hosting.panel.restarting"),
  );
}

function togglePower() {
  const on = !server.value?.enabled;
  return run("power", async () => {
    server.value = await hostedApi<HostedServer>(
      `/hosted-servers/${hostedId.value}/power`,
      { method: "POST", body: { on } },
    );
  });
}

function saveSettings() {
  return run(
    "settings",
    async () => {
      server.value = await hostedApi<HostedServer>(
        `/hosted-servers/${hostedId.value}/settings`,
        {
          method: "POST",
          body: {
            label: form.value.label,
            connect_password: form.value.connect_password,
            type: form.value.type,
          },
        },
      );
      syncForm();
    },
    t("pages.hosting.panel.saved"),
  );
}

async function sendRcon(command: string) {
  const trimmed = command.trim();
  if (!trimmed) return;
  await run("rcon", async () => {
    const { result } = await hostedApi<{ result: string }>(
      `/hosted-servers/${hostedId.value}/rcon`,
      { method: "POST", body: { command: trimmed } },
    );
    rconLog.value = [{ command: trimmed, result }, ...rconLog.value].slice(
      0,
      30,
    );
    if (command === rconCommand.value) {
      rconCommand.value = "";
    }
  });
}

async function copyConnect() {
  if (!connectCommand.value) return;
  await navigator.clipboard.writeText(connectCommand.value);
  toast({ title: t("pages.hosting.panel.copied") });
}

function openRenew() {
  renewPlanId.value = renewPlans.value[0]?.id || "";
  renewTerms.value = false;
  renewOpen.value = true;
}

function renew() {
  return run("renew", async () => {
    const result = await hostedApi<{ deepLink: string }>(
      "/store/hosted-checkout",
      {
        method: "POST",
        body: {
          productId: renewPlanId.value,
          hostedServerId: hostedId.value,
          termsAccepted: true,
        },
      },
    );
    renewOpen.value = false;
    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.hosting.checkout_hint"),
    });
    window.open(result.deepLink, "_blank", "noopener,noreferrer");
  });
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

let poll: number | undefined;
onMounted(async () => {
  await load();
  try {
    overview.value = await hostedApi<HostedOverview>(
      "/hosted-servers/overview",
    );
  } catch {
    overview.value = null;
  }
  poll = window.setInterval(() => void load(true), 15_000);
});
onUnmounted(() => window.clearInterval(poll));
</script>

<template>
  <div class="space-y-6 pb-16">
    <TacticalPageHeader>
      <template #title>{{
        server?.label || $t("pages.hosting.title")
      }}</template>
      <template #subtitle>{{ $t("pages.hosting.panel.subtitle") }}</template>
    </TacticalPageHeader>

    <PageTransition>
      <div v-if="loading" class="grid gap-4 lg:grid-cols-2">
        <Skeleton class="h-48 rounded-lg" />
        <Skeleton class="h-48 rounded-lg" />
      </div>

      <p v-else-if="!server" class="text-sm text-muted-foreground">
        {{ $t("pages.hosting.panel.not_found") }}
      </p>

      <div v-else class="grid gap-4 lg:grid-cols-2">
        <section
          class="space-y-4 rounded-lg border border-border bg-card/40 p-4"
        >
          <div class="flex flex-wrap items-center gap-2">
            <Badge :variant="hostedStatusVariant(server.status)">
              {{ $t(`pages.hosting.status.${server.status}`) }}
            </Badge>
            <Badge
              v-if="isActive"
              :variant="server.connected ? 'default' : 'outline'"
            >
              {{
                !server.enabled
                  ? $t("pages.hosting.panel.stopped")
                  : server.connected
                    ? $t("pages.hosting.panel.online")
                    : $t("pages.hosting.panel.starting")
              }}
            </Badge>
            <Badge variant="secondary">
              {{ $t("pages.hosting.slots", { n: server.slots }) }}
            </Badge>
            <Badge v-if="server.type" variant="outline">
              {{ $t(`pages.hosting.modes.${server.type}`) }}
            </Badge>
          </div>

          <p
            v-if="server.status_detail"
            class="m-0 rounded-md border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-xs"
          >
            {{ server.status_detail }}
          </p>

          <dl class="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt class="text-muted-foreground">
              {{ $t("pages.hosting.panel.address") }}
            </dt>
            <dd class="m-0 font-mono" dir="ltr">
              {{
                server.host && server.port
                  ? `${server.host}:${server.port}`
                  : "—"
              }}
            </dd>
            <dt class="text-muted-foreground">
              {{ $t("pages.hosting.panel.expires") }}
            </dt>
            <dd class="m-0">
              {{ formatHostedDate(server.expires_at, locale) }}
            </dd>
            <template v-if="server.players !== null">
              <dt class="text-muted-foreground">
                {{ $t("pages.hosting.panel.players") }}
              </dt>
              <dd class="m-0">
                {{ server.players }}/{{ server.slots }}
                <span v-if="server.map" class="font-mono text-muted-foreground">
                  · {{ server.map }}
                </span>
              </dd>
            </template>
          </dl>

          <div
            v-if="connectCommand"
            class="flex items-center gap-2 rounded-md border border-border bg-muted/30 px-3 py-2"
          >
            <code class="min-w-0 flex-1 truncate text-xs" dir="ltr">
              {{ connectCommand }}
            </code>
            <Button
              size="icon"
              variant="ghost"
              class="h-7 w-7"
              @click="copyConnect"
            >
              <Copy class="h-3.5 w-3.5" />
            </Button>
          </div>

          <div class="flex flex-wrap gap-2">
            <Button
              v-if="isActive"
              size="sm"
              variant="outline"
              :disabled="!!busy || !server.enabled"
              @click="restart"
            >
              <RotateCcw class="me-1.5 h-3.5 w-3.5" />
              {{ $t("pages.hosting.panel.restart") }}
            </Button>
            <Button
              v-if="isActive"
              size="sm"
              :variant="server.enabled ? 'destructive' : 'default'"
              :disabled="!!busy"
              @click="togglePower"
            >
              <Power class="me-1.5 h-3.5 w-3.5" />
              {{
                server.enabled
                  ? $t("pages.hosting.panel.stop")
                  : $t("pages.hosting.panel.start")
              }}
            </Button>
            <Button
              v-if="canRenew"
              size="sm"
              variant="secondary"
              :disabled="!!busy || !renewPlans.length"
              @click="openRenew"
            >
              <RefreshCw class="me-1.5 h-3.5 w-3.5" />
              {{ $t("pages.hosting.panel.renew") }}
            </Button>
          </div>
        </section>

        <section
          v-if="isActive"
          class="space-y-4 rounded-lg border border-border bg-card/40 p-4"
        >
          <h2 class="m-0 text-base font-semibold">
            {{ $t("pages.hosting.panel.settings") }}
          </h2>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.hosting.server_name") }}</Label>
            <Input v-model="form.label" maxlength="64" />
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.hosting.panel.password") }}</Label>
            <Input
              v-model="form.connect_password"
              maxlength="32"
              dir="ltr"
              :placeholder="$t('pages.hosting.panel.password_placeholder')"
            />
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.hosting.mode") }}</Label>
            <Select v-model="form.type">
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem
                  v-for="type in overview?.types || []"
                  :key="type"
                  :value="type"
                >
                  {{ $t(`pages.hosting.modes.${type}`) }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <p class="m-0 text-xs text-muted-foreground">
            {{ $t("pages.hosting.panel.settings_restart_note") }}
          </p>
          <Button size="sm" :disabled="!!busy" @click="saveSettings">
            {{ $t("pages.hosting.panel.save") }}
          </Button>
        </section>

        <section
          v-if="isActive"
          class="space-y-3 rounded-lg border border-border bg-card/40 p-4 lg:col-span-2"
        >
          <h2 class="m-0 flex items-center gap-2 text-base font-semibold">
            <Terminal class="h-4 w-4" />
            {{ $t("pages.hosting.panel.console") }}
          </h2>

          <div class="flex flex-wrap items-center gap-2">
            <Select v-model="mapToLoad">
              <SelectTrigger class="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem v-for="map in MAPS" :key="map" :value="map">
                  {{ map }}
                </SelectItem>
              </SelectContent>
            </Select>
            <Button
              size="sm"
              variant="outline"
              :disabled="!!busy || !server.connected"
              @click="sendRcon(`changelevel ${mapToLoad}`)"
            >
              {{ $t("pages.hosting.panel.change_map") }}
            </Button>
            <Button
              size="sm"
              variant="outline"
              :disabled="!!busy || !server.connected"
              @click="sendRcon('mp_restartgame 1')"
            >
              {{ $t("pages.hosting.panel.restart_game") }}
            </Button>
            <Button
              size="sm"
              variant="outline"
              :disabled="!!busy || !server.connected"
              @click="sendRcon('status')"
            >
              status
            </Button>
          </div>

          <form
            class="flex gap-2"
            dir="ltr"
            @submit.prevent="sendRcon(rconCommand)"
          >
            <Input
              v-model="rconCommand"
              class="font-mono"
              maxlength="512"
              placeholder="mp_roundtime 2"
            />
            <Button
              type="submit"
              size="sm"
              :disabled="!!busy || !server.connected || !rconCommand.trim()"
            >
              <Send class="h-3.5 w-3.5" />
            </Button>
          </form>

          <div
            v-if="rconLog.length"
            class="max-h-80 space-y-2 overflow-y-auto rounded-md bg-black/80 p-3 font-mono text-xs text-green-300"
            dir="ltr"
          >
            <div v-for="(entry, index) in rconLog" :key="index">
              <div class="text-white">&gt; {{ entry.command }}</div>
              <pre class="m-0 whitespace-pre-wrap">{{
                entry.result || "(ok)"
              }}</pre>
            </div>
          </div>
        </section>
      </div>
    </PageTransition>

    <Dialog v-model:open="renewOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ $t("pages.hosting.panel.renew") }}</DialogTitle>
          <DialogDescription>{{ server?.label }}</DialogDescription>
        </DialogHeader>
        <div class="space-y-3">
          <button
            v-for="plan in renewPlans"
            :key="plan.id"
            type="button"
            class="flex w-full items-center justify-between rounded-lg border px-3 py-2.5 text-start"
            :class="
              renewPlanId === plan.id
                ? 'border-primary/60 bg-primary/5'
                : 'border-border'
            "
            @click="renewPlanId = plan.id"
          >
            <span class="text-sm font-medium">
              {{ plan.title }} · {{ formatHostedDuration(plan.duration, t) }}
            </span>
            <span class="font-mono text-sm tabular-nums">
              {{ formatPrice(plan.price_irr) }}
            </span>
          </button>
          <button
            type="button"
            class="flex w-full items-start gap-3 rounded-lg border border-border px-3 py-3 text-start"
            @click="renewTerms = !renewTerms"
          >
            <Checkbox
              :model-value="renewTerms"
              class="pointer-events-none mt-0.5"
              tabindex="-1"
            />
            <span class="text-sm leading-snug">
              {{ $t("pages.store.terms_accept") }}
            </span>
          </button>
        </div>
        <DialogFooter class="gap-2">
          <Button variant="ghost" :disabled="!!busy" @click="renewOpen = false">
            {{ $t("common.cancel") }}
          </Button>
          <Button
            :disabled="!!busy || !renewTerms || !renewPlanId"
            @click="renew"
          >
            {{ $t("pages.store.pay_with_bale") }}
          </Button>
        </DialogFooter>
      </DialogScrollContent>
    </Dialog>
  </div>
</template>
