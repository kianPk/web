<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  MoreHorizontal,
  RefreshCw,
  Search,
  ServerCog,
  Settings2,
  Tags,
} from "lucide-vue-next";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "~/components/ui/dropdown-menu";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "~/components/ui/tabs";
import { toast } from "~/components/ui/toast";
import {
  formatHostedDate,
  formatHostedDuration,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import { formatTomanAmount } from "~/utilities/irrToman";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedAdminPlan,
  type HostedAdminSettings,
  type HostedServer,
} from "~/composables/useHostedServers";

const emit = defineEmits<{ changed: [] }>();
const { t, locale } = useI18n();

const tab = ref<"servers" | "settings" | "plans">("servers");
const plans = ref<HostedAdminPlan[]>([]);
const servers = ref<HostedServer[]>([]);
const showDeleted = ref(false);
const serverQuery = ref("");
const deletedCount = computed(
  () => servers.value.filter((s) => s.status === "deleted").length,
);
const visibleServers = computed(() => {
  const base = showDeleted.value
    ? servers.value
    : servers.value.filter((s) => s.status !== "deleted");
  const q = serverQuery.value.trim().toLowerCase();
  if (!q) return base;
  return base.filter((s) => {
    const hay = [
      s.label,
      s.owner_name,
      s.owner_steam_id,
      s.type,
      s.host,
      s.status,
      s.status_detail,
    ]
      .filter(Boolean)
      .join(" ")
      .toLowerCase();
    return hay.includes(q);
  });
});
const settings = ref<HostedAdminSettings | null>(null);
const form = ref({
  enabled: true,
  max_active: 3,
  reserve_match_slots: 2,
  grace_days: 3,
  slot_price_toman: 0,
  slot_price_ypoint: 0,
  max_slots: 32,
});
const busy = ref(false);
const loading = ref(true);

async function load() {
  try {
    const [list, current, planList] = await Promise.all([
      hostedApi<HostedServer[]>("/hosted-servers/admin/list"),
      hostedApi<HostedAdminSettings>("/hosted-servers/admin/settings"),
      hostedApi<HostedAdminPlan[]>("/hosted-servers/admin/plans"),
    ]);
    servers.value = list;
    plans.value = planList;
    applySettings(current);
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    loading.value = false;
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
    slot_price_toman: Math.round((current.slot_price_irr || 0) / 10),
    slot_price_ypoint: current.slot_price_ypoint || 0,
    max_slots: current.max_slots || 32,
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
      slot_price_irr: Math.round(Number(form.value.slot_price_toman) * 10),
      slot_price_ypoint: Number(form.value.slot_price_ypoint),
      max_slots: Number(form.value.max_slots),
    };
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
  return act(async () => {
    await hostedApi(`/hosted-servers/admin/${server.id}/purge`, {
      method: "POST",
    });
    toast({ title: t("pages.hosting.admin.server_deleted") });
  });
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

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

function setPlanActive(plan: HostedAdminPlan, active: boolean) {
  return act(() =>
    hostedApi(`/hosted-servers/admin/plans/${plan.id}/active`, {
      method: "POST",
      body: { active },
    }),
  );
}

function removePlan(plan: HostedAdminPlan) {
  if (!window.confirm(t("pages.hosting.admin.plan_delete_confirm"))) return;
  return act(async () => {
    await hostedApi(`/hosted-servers/admin/plans/${plan.id}/delete`, {
      method: "POST",
    });
    toast({ title: t("pages.hosting.admin.plan_deleted") });
  });
}

function serverAddress(server: HostedServer) {
  if (!server.host || !server.port) return null;
  return `${server.host}:${server.port}`;
}

onMounted(() => {
  void load();
});
</script>

<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="space-y-1">
        <h2 class="m-0 text-lg font-semibold tracking-tight">
          {{ $t("pages.hosting.admin.title") }}
        </h2>
        <p class="m-0 text-sm text-muted-foreground">
          {{ $t("pages.hosting.admin.panel_blurb") }}
        </p>
      </div>
      <Button
        size="sm"
        variant="outline"
        :disabled="busy || loading"
        @click="load"
      >
        <RefreshCw class="me-1.5 h-3.5 w-3.5" />
        {{ $t("pages.hosting.admin.refresh") }}
      </Button>
    </div>

    <Tabs v-model="tab" class="space-y-4">
      <TabsList class="grid h-auto w-full grid-cols-3 gap-1 p-1 sm:w-auto sm:inline-grid">
        <TabsTrigger value="servers" class="gap-1.5">
          <ServerCog class="h-3.5 w-3.5" />
          {{
            $t("pages.hosting.admin.tab_servers", {
              n: servers.length - deletedCount,
            })
          }}
        </TabsTrigger>
        <TabsTrigger value="settings" class="gap-1.5">
          <Settings2 class="h-3.5 w-3.5" />
          {{ $t("pages.hosting.admin.tab_settings") }}
        </TabsTrigger>
        <TabsTrigger value="plans" class="gap-1.5">
          <Tags class="h-3.5 w-3.5" />
          {{ $t("pages.hosting.admin.tab_plans", { n: plans.length }) }}
        </TabsTrigger>
      </TabsList>

      <TabsContent value="servers" class="mt-0 space-y-3 outline-none">
        <div class="flex flex-wrap items-center gap-2">
          <div class="relative min-w-[12rem] flex-1">
            <Search
              class="pointer-events-none absolute start-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground"
            />
            <Input
              v-model="serverQuery"
              class="h-9 ps-8"
              :placeholder="$t('pages.hosting.admin.search_placeholder')"
            />
          </div>
          <Button
            v-if="deletedCount"
            size="sm"
            variant="ghost"
            @click="showDeleted = !showDeleted"
          >
            {{
              showDeleted
                ? $t("pages.hosting.admin.hide_deleted")
                : $t("pages.hosting.admin.show_deleted", { n: deletedCount })
            }}
          </Button>
        </div>

        <div
          v-if="visibleServers.length"
          class="grid gap-2.5 sm:grid-cols-2 xl:grid-cols-3"
        >
          <article
            v-for="server in visibleServers"
            :key="server.id"
            class="group flex flex-col gap-3 rounded-xl border border-border/70 bg-card/50 p-3.5 transition-colors hover:border-border hover:bg-card/80"
          >
            <div class="flex items-start gap-3">
              <div
                class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted/60 text-muted-foreground"
              >
                <ServerCog class="h-5 w-5" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="flex items-start justify-between gap-2">
                  <div class="min-w-0">
                    <NuxtLink
                      :to="`/hosting/${server.id}`"
                      class="block truncate font-medium hover:underline"
                    >
                      {{ server.label }}
                    </NuxtLink>
                    <p class="m-0 mt-0.5 truncate text-xs text-muted-foreground">
                      <NuxtLink
                        :to="`/players/${server.owner_steam_id}`"
                        class="hover:underline"
                      >
                        {{ server.owner_name || server.owner_steam_id }}
                      </NuxtLink>
                      <span v-if="server.type"> · {{ server.type }}</span>
                    </p>
                  </div>
                  <DropdownMenu>
                    <DropdownMenuTrigger as-child>
                      <Button
                        size="icon"
                        variant="ghost"
                        class="h-8 w-8 shrink-0"
                        :disabled="busy"
                      >
                        <MoreHorizontal class="h-4 w-4" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" class="w-44">
                      <DropdownMenuItem as-child>
                        <NuxtLink :to="`/hosting/${server.id}`">
                          {{ $t("pages.hosting.admin.open_panel") }}
                        </NuxtLink>
                      </DropdownMenuItem>
                      <DropdownMenuItem @click="extend(server)">
                        {{ $t("pages.hosting.admin.extend") }}
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        v-if="server.server_id"
                        @click="setGslt(server)"
                      >
                        GSLT
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        v-if="
                          server.status === 'active' ||
                          server.status === 'expired'
                        "
                        @click="suspend(server, true)"
                      >
                        {{ $t("pages.hosting.admin.suspend") }}
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        v-if="server.status === 'suspended'"
                        @click="suspend(server, false)"
                      >
                        {{ $t("pages.hosting.admin.unsuspend") }}
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        v-if="
                          server.status === 'failed' ||
                          server.status === 'deleted'
                        "
                        @click="retry(server)"
                      >
                        {{ $t("pages.hosting.admin.retry") }}
                      </DropdownMenuItem>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem
                        class="text-destructive focus:text-destructive"
                        @click="remove(server)"
                      >
                        {{ $t("pages.hosting.admin.delete") }}
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              </div>
            </div>

            <div class="flex flex-wrap items-center gap-1.5">
              <Badge :variant="hostedStatusVariant(server.status)">
                {{ $t(`pages.hosting.status.${server.status}`) }}
              </Badge>
              <Badge variant="outline" class="font-normal">
                {{ $t("pages.hosting.slots", { n: server.slots }) }}
              </Badge>
              <Badge
                v-if="server.server_id"
                variant="outline"
                class="font-normal"
              >
                GSLT {{ server.has_gslt ? "✓" : "✗" }}
              </Badge>
            </div>

            <div
              class="mt-auto flex flex-wrap items-center justify-between gap-2 border-t border-border/50 pt-2.5 text-[0.7rem] text-muted-foreground"
            >
              <span>
                {{ formatHostedDate(server.expires_at, locale) }}
              </span>
              <code v-if="serverAddress(server)" class="font-mono" dir="ltr">
                {{ serverAddress(server) }}
              </code>
            </div>
            <p
              v-if="server.status_detail"
              class="m-0 line-clamp-2 text-xs text-muted-foreground"
            >
              {{ server.status_detail }}
            </p>
          </article>
        </div>
        <p
          v-else
          class="m-0 rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
        >
          {{ $t("pages.hosting.no_servers") }}
        </p>
      </TabsContent>

      <TabsContent value="settings" class="mt-0 outline-none">
        <div
          v-if="settings"
          class="space-y-4 rounded-xl border border-border/70 bg-card/40 p-4"
        >
          <button
            type="button"
            class="flex w-full items-center justify-between gap-3 rounded-lg border border-border/70 bg-background/40 px-3 py-2.5 text-start text-sm transition-colors hover:bg-muted/30"
            @click="form.enabled = !form.enabled"
          >
            <span>{{ $t("pages.hosting.admin.sales_enabled") }}</span>
            <Checkbox
              :model-value="form.enabled"
              class="pointer-events-none"
              tabindex="-1"
            />
          </button>

          <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.admin.max_active") }}</Label>
              <Input v-model="form.max_active" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.admin.reserve_match_slots") }}</Label>
              <Input v-model="form.reserve_match_slots" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.admin.grace_days") }}</Label>
              <Input v-model="form.grace_days" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.admin.slot_price_toman") }}</Label>
              <Input v-model="form.slot_price_toman" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.admin.slot_price_ypoint") }}</Label>
              <Input v-model="form.slot_price_ypoint" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.admin.max_slots") }}</Label>
              <Input v-model="form.max_slots" type="number" min="2" max="64" />
            </div>
          </div>
          <p class="m-0 text-xs text-muted-foreground">
            {{ $t("pages.hosting.admin.slot_price_hint") }}
          </p>
          <Button size="sm" :disabled="busy" @click="saveSettings">
            {{ $t("pages.hosting.panel.save") }}
          </Button>
        </div>
      </TabsContent>

      <TabsContent value="plans" class="mt-0 space-y-3 outline-none">
        <div class="flex items-center justify-between gap-2">
          <p class="m-0 text-sm text-muted-foreground">
            {{ $t("pages.hosting.admin.plans") }}
          </p>
          <Button size="sm" variant="outline" as-child>
            <NuxtLink to="/settings/application/store">
              {{ $t("pages.hosting.admin.plan_add") }}
            </NuxtLink>
          </Button>
        </div>
        <p
          v-if="!plans.length"
          class="m-0 rounded-xl border border-dashed border-border/70 px-4 py-10 text-center text-sm text-muted-foreground"
        >
          {{ $t("pages.hosting.admin.plans_empty") }}
        </p>
        <ul v-else class="m-0 grid list-none gap-2.5 p-0 sm:grid-cols-2">
          <li
            v-for="plan in plans"
            :key="plan.id"
            class="flex flex-col gap-3 rounded-xl border border-border/70 bg-card/50 p-3.5"
          >
            <div class="flex items-start justify-between gap-2">
              <div class="min-w-0">
                <div class="truncate font-medium">{{ plan.title }}</div>
                <p class="m-0 mt-0.5 text-xs text-muted-foreground">
                  {{ $t("pages.hosting.slots", { n: plan.hosted_slots }) }}
                  · {{ formatHostedDuration(plan.duration, t) }}
                  · {{ formatPrice(plan.price_irr) }}
                  <span v-if="plan.price_ypoint">
                    · {{ plan.price_ypoint }} Ypoint
                  </span>
                </p>
              </div>
              <Badge :variant="plan.active ? 'default' : 'secondary'">
                {{
                  plan.active
                    ? $t("pages.hosting.admin.plan_active")
                    : $t("pages.hosting.admin.plan_inactive")
                }}
              </Badge>
            </div>
            <div
              class="flex flex-wrap items-center justify-between gap-2 border-t border-border/50 pt-2.5"
            >
              <span class="text-xs text-muted-foreground">
                {{ $t("pages.hosting.admin.plan_servers", { n: plan.servers }) }}
              </span>
              <div class="flex gap-1.5">
                <Button
                  size="sm"
                  variant="outline"
                  :disabled="busy"
                  @click="setPlanActive(plan, !plan.active)"
                >
                  {{
                    plan.active
                      ? $t("pages.hosting.admin.plan_disable")
                      : $t("pages.hosting.admin.plan_enable")
                  }}
                </Button>
                <Button
                  size="sm"
                  variant="destructive"
                  :disabled="busy"
                  @click="removePlan(plan)"
                >
                  {{ $t("pages.hosting.admin.delete") }}
                </Button>
              </div>
            </div>
          </li>
        </ul>
      </TabsContent>
    </Tabs>
  </section>
</template>
