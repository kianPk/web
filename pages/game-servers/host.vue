<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { ArrowLeft, Clock, Pencil, Plus, Server, Trash2 } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Skeleton } from "~/components/ui/skeleton";
import Empty from "~/components/ui/empty/Empty.vue";
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
import { formatTomanAmount, irrToToman, tomanToIrr } from "~/utilities/irrToman";
import {
  formatHostedDate,
  formatHostedDuration,
  hostedStatusVariant,
} from "~/utilities/hostedFormat";
import { SERVER_MODES, type ServerModeKey } from "~/utilities/serverModes";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedCheckoutResult,
  type HostedPlan,
  type HostedServer,
  type SectionHostOverview,
} from "~/composables/useHostedServers";
import { useAuthStore } from "~/stores/AuthStore";

type AdminHostPlan = HostedPlan & {
  description: string;
  active: boolean;
  sort_order: number;
  servers: number;
};

const DURATION_PRESETS = ["1h", "3h", "12h", "1d", "3d", "7d", "14d", "30d"];

const { t, locale } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

useHead(() => ({ title: t("pages.servers.host.title") }));

const overview = ref<SectionHostOverview | null>(null);
const myServers = ref<HostedServer[]>([]);
const adminPlans = ref<AdminHostPlan[]>([]);
const loading = ref(true);

const buyPlan = ref<HostedPlan | null>(null);
const buyOpen = ref(false);
const buyMode = ref<ServerModeKey>("duels");
const buyLabel = ref("");
const termsAccepted = ref(false);
const paying = ref(false);

const planEditorOpen = ref(false);
const planSaving = ref(false);
const editingPlanId = ref<string | null>(null);
const planForm = ref({
  title: "",
  description: "",
  duration: "1d",
  price_toman: 0,
  price_ypoint: null as number | null,
  sort_order: 0,
  active: true,
});

const signedIn = computed(() => Boolean(auth.me?.steam_id));
const isAdmin = computed(() => auth.isAdmin);

const modeOptions = computed(() => {
  const allowed = new Set(
    overview.value?.modes || SERVER_MODES.map((m) => m.key),
  );
  return SERVER_MODES.filter((m) => allowed.has(m.key));
});

const myHostServers = computed(() =>
  myServers.value.filter((s) => Boolean(s.section_mode)),
);

async function refresh() {
  loading.value = true;
  try {
    overview.value = await hostedApi<SectionHostOverview>(
      "/hosted-servers/section-host/overview",
    );
  } catch (error) {
    console.error(error);
    overview.value = null;
  }
  if (signedIn.value) {
    try {
      myServers.value = await hostedApi<HostedServer[]>("/hosted-servers/mine");
    } catch {
      myServers.value = [];
    }
  }
  if (isAdmin.value) {
    try {
      adminPlans.value = await hostedApi<AdminHostPlan[]>(
        "/hosted-servers/admin/section-plans",
      );
    } catch {
      adminPlans.value = [];
    }
  }
  loading.value = false;
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

function openBuy(plan: HostedPlan) {
  if (!signedIn.value) {
    toast({
      variant: "destructive",
      title: t("pages.store.sign_in_required"),
    });
    return;
  }
  buyPlan.value = plan;
  buyMode.value = modeOptions.value[0]?.key || "duels";
  buyLabel.value = "";
  termsAccepted.value = false;
  buyOpen.value = true;
}

const canAffordPlan = computed(() => {
  const price = Number(buyPlan.value?.price_ypoint || 0);
  return price > 0 && (ypointBalance.value ?? 0) >= price;
});

async function pay(payWith: "bale" | "ypoint" = "bale") {
  if (!buyPlan.value || paying.value) return;
  if (!termsAccepted.value) {
    toast({ variant: "destructive", title: t("pages.store.terms_required") });
    return;
  }
  paying.value = true;
  try {
    const result = await hostedApi<HostedCheckoutResult>(
      "/hosted-servers/checkout",
      {
        method: "POST",
        body: {
          productId: buyPlan.value.id,
          sectionMode: buyMode.value,
          label: buyLabel.value,
          termsAccepted: true,
          payWith,
        },
      },
    );
    buyOpen.value = false;
    if (result.paid) {
      toast({ title: t("pages.hosting.ypoint_paid") });
      void refreshYpoints();
      if (result.hostedServerId) {
        await router.push(`/hosting/${result.hostedServerId}`);
      } else {
        await refresh();
      }
      return;
    }
    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.servers.host.checkout_hint"),
    });
    window.open(result.deepLink, "_blank", "noopener,noreferrer");
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: hostedErrorMessage(error),
    });
  } finally {
    paying.value = false;
  }
}

function openCreatePlan() {
  editingPlanId.value = null;
  planForm.value = {
    title: "",
    description: "",
    duration: "1d",
    price_toman: 0,
    price_ypoint: null,
    sort_order: (adminPlans.value.at(-1)?.sort_order ?? 200) + 1,
    active: true,
  };
  planEditorOpen.value = true;
}

function openEditPlan(plan: AdminHostPlan) {
  editingPlanId.value = plan.id;
  planForm.value = {
    title: plan.title,
    description: plan.description || "",
    duration: plan.duration || "1d",
    price_toman: irrToToman(plan.price_irr),
    price_ypoint: plan.price_ypoint,
    sort_order: plan.sort_order ?? 0,
    active: plan.active,
  };
  planEditorOpen.value = true;
}

async function savePlan() {
  if (planSaving.value) return;
  if (!planForm.value.title.trim()) {
    toast({
      variant: "destructive",
      title: t("pages.servers.host.admin.title_required"),
    });
    return;
  }
  planSaving.value = true;
  try {
    const body = {
      title: planForm.value.title.trim(),
      description: planForm.value.description.trim(),
      duration: planForm.value.duration.trim(),
      price_irr: tomanToIrr(planForm.value.price_toman),
      price_ypoint:
        planForm.value.price_ypoint == null
          ? null
          : Math.max(0, Math.round(Number(planForm.value.price_ypoint) || 0)) ||
            null,
      sort_order: Math.round(Number(planForm.value.sort_order) || 0),
      active: planForm.value.active,
    };
    if (editingPlanId.value) {
      await hostedApi(
        `/hosted-servers/admin/section-plans/${editingPlanId.value}`,
        {
          method: "POST",
          body,
        },
      );
    } else {
      await hostedApi("/hosted-servers/admin/section-plans", {
        method: "POST",
        body,
      });
    }
    planEditorOpen.value = false;
    toast({ title: t("pages.servers.host.admin.saved") });
    await refresh();
  } catch (error) {
    toast({
      variant: "destructive",
      title: t("pages.servers.host.admin.save_failed"),
      description: hostedErrorMessage(error),
    });
  } finally {
    planSaving.value = false;
  }
}

async function togglePlanActive(plan: AdminHostPlan) {
  try {
    await hostedApi(`/hosted-servers/admin/section-plans/${plan.id}/active`, {
      method: "POST",
      body: { active: !plan.active },
    });
    await refresh();
  } catch (error) {
    toast({
      variant: "destructive",
      title: hostedErrorMessage(error),
    });
  }
}

async function deletePlan(plan: AdminHostPlan) {
  if (
    !confirm(
      t("pages.servers.host.admin.delete_confirm", { title: plan.title }),
    )
  ) {
    return;
  }
  try {
    await hostedApi(
      `/hosted-servers/admin/section-plans/${plan.id}/delete`,
      { method: "POST" },
    );
    toast({ title: t("pages.servers.host.admin.deleted") });
    await refresh();
  } catch (error) {
    toast({
      variant: "destructive",
      title: hostedErrorMessage(error),
    });
  }
}

onMounted(() => {
  void refresh();
  void refreshYpoints();
});
</script>

<template>
  <div class="space-y-8 pb-16">
    <div class="space-y-3">
      <NuxtLink
        to="/game-servers"
        class="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeft class="h-4 w-4" />
        {{ $t("pages.servers.back_to_modes") }}
      </NuxtLink>
      <div class="space-y-1">
        <h1 class="m-0 text-2xl font-semibold tracking-tight">
          {{ $t("pages.servers.host.title") }}
        </h1>
        <p class="m-0 max-w-2xl text-sm text-muted-foreground">
          {{ $t("pages.servers.host.description") }}
        </p>
      </div>
    </div>

    <PageTransition>
      <div v-if="loading" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton v-for="i in 3" :key="i" class="h-56 rounded-lg" />
      </div>

      <div v-else class="space-y-8">
        <section
          v-if="isAdmin"
          class="space-y-3 rounded-xl border border-border/70 bg-card/30 p-4"
        >
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="m-0 text-base font-semibold">
                {{ $t("pages.servers.host.admin.title") }}
              </h2>
              <p class="m-0 mt-0.5 text-xs text-muted-foreground">
                {{ $t("pages.servers.host.admin.hint") }}
              </p>
            </div>
            <Button type="button" size="sm" @click="openCreatePlan">
              <Plus class="h-4 w-4" />
              {{ $t("pages.servers.host.admin.add") }}
            </Button>
          </div>

          <p
            v-if="!adminPlans.length"
            class="m-0 text-sm text-muted-foreground"
          >
            {{ $t("pages.servers.host.admin.empty") }}
          </p>
          <div v-else class="space-y-2">
            <div
              v-for="plan in adminPlans"
              :key="plan.id"
              class="flex flex-wrap items-center gap-3 rounded-lg border border-border/60 bg-background/40 px-3 py-2.5"
            >
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="truncate font-medium">{{ plan.title }}</span>
                  <Badge :variant="plan.active ? 'default' : 'secondary'">
                    {{
                      plan.active
                        ? $t("pages.servers.host.admin.active")
                        : $t("pages.servers.host.admin.inactive")
                    }}
                  </Badge>
                  <Badge variant="outline">
                    {{ formatHostedDuration(plan.duration, t) }}
                  </Badge>
                </div>
                <p class="m-0 mt-1 text-xs text-muted-foreground">
                  {{ formatPrice(plan.price_irr) }}
                  <span v-if="plan.price_ypoint">
                    · {{ plan.price_ypoint }} YP
                  </span>
                  · {{ $t("pages.servers.host.admin.rentals", { n: plan.servers }) }}
                </p>
              </div>
              <div class="flex flex-wrap gap-1.5">
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  @click="togglePlanActive(plan)"
                >
                  {{
                    plan.active
                      ? $t("pages.servers.host.admin.disable")
                      : $t("pages.servers.host.admin.enable")
                  }}
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  @click="openEditPlan(plan)"
                >
                  <Pencil class="h-3.5 w-3.5" />
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  @click="deletePlan(plan)"
                >
                  <Trash2 class="h-3.5 w-3.5" />
                </Button>
              </div>
            </div>
          </div>
        </section>

        <div
          v-if="overview && (!overview.enabled || !overview.available)"
          class="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm"
        >
          {{
            !overview.enabled
              ? $t("pages.servers.host.sales_closed")
              : $t("pages.servers.host.capacity_full")
          }}
        </div>
        <p v-else-if="overview" class="m-0 text-xs text-muted-foreground">
          {{ $t("pages.servers.host.remaining", { n: overview.remaining }) }}
        </p>

        <Empty v-if="!overview?.plans?.length">
          <h2 class="m-0 text-lg font-semibold">
            {{ $t("pages.servers.host.no_plans") }}
          </h2>
        </Empty>

        <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <article
            v-for="plan in overview.plans"
            :key="plan.id"
            class="flex flex-col overflow-hidden rounded-lg border border-border bg-card/40"
          >
            <div class="flex flex-1 flex-col gap-3 p-4">
              <h2 class="m-0 font-sans text-base font-semibold">
                {{ plan.title }}
              </h2>
              <div class="flex flex-wrap gap-2">
                <Badge variant="outline">
                  <Clock class="h-3 w-3" />
                  {{ formatHostedDuration(plan.duration, t) }}
                </Badge>
              </div>
              <p
                v-if="plan.description"
                class="m-0 whitespace-pre-line text-sm text-muted-foreground"
              >
                {{ plan.description }}
              </p>
              <div class="mt-auto flex items-center justify-between gap-3">
                <div class="flex flex-col gap-0.5">
                  <span class="font-mono text-sm font-semibold tabular-nums">
                    {{ formatPrice(plan.price_irr) }}
                  </span>
                  <span
                    v-if="plan.price_ypoint"
                    class="inline-flex items-center gap-1 font-mono text-xs font-semibold tabular-nums text-[hsl(var(--tac-amber))]"
                  >
                    <img
                      src="/img/ypoint-logo.png"
                      alt="Ypoint"
                      class="h-3.5 w-3.5 object-contain"
                    />
                    {{ plan.price_ypoint }}
                  </span>
                </div>
                <Button
                  type="button"
                  size="sm"
                  :disabled="!overview?.available"
                  @click="openBuy(plan)"
                >
                  {{ $t("pages.servers.host.rent") }}
                </Button>
              </div>
            </div>
          </article>
        </div>

        <section v-if="signedIn" class="space-y-3">
          <h2 class="m-0 text-lg font-semibold">
            {{ $t("pages.servers.host.my_servers") }}
          </h2>
          <p
            v-if="!myHostServers.length"
            class="m-0 text-sm text-muted-foreground"
          >
            {{ $t("pages.servers.host.no_servers") }}
          </p>
          <div v-else class="grid gap-3 sm:grid-cols-2">
            <NuxtLink
              v-for="server in myHostServers"
              :key="server.id"
              :to="`/hosting/${server.id}`"
              class="flex items-center gap-3 rounded-lg border border-border bg-card/40 p-4 transition-colors hover:bg-muted/40"
            >
              <Server class="h-8 w-8 shrink-0 text-muted-foreground" />
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <span class="truncate font-semibold">{{ server.label }}</span>
                  <Badge :variant="hostedStatusVariant(server.status)">
                    {{ $t(`pages.hosting.status.${server.status}`) }}
                  </Badge>
                  <Badge v-if="server.section_mode" variant="outline">
                    {{
                      $t(`pages.servers.modes.${server.section_mode}.name`)
                    }}
                  </Badge>
                </div>
                <p class="m-0 mt-1 text-xs text-muted-foreground">
                  {{
                    $t("pages.hosting.expires_at", {
                      date: formatHostedDate(server.expires_at, locale),
                    })
                  }}
                </p>
              </div>
            </NuxtLink>
          </div>
        </section>
      </div>
    </PageTransition>

    <Dialog v-model:open="buyOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ buyPlan?.title }}</DialogTitle>
          <DialogDescription>
            {{ buyPlan ? formatPrice(buyPlan.price_irr) : "" }}
            ·
            {{ buyPlan ? formatHostedDuration(buyPlan.duration, t) : "" }}
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-4">
          <div class="space-y-1.5">
            <Label>{{ $t("pages.servers.host.pick_mode") }}</Label>
            <Select v-model="buyMode">
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem
                  v-for="mode in modeOptions"
                  :key="mode.key"
                  :value="mode.key"
                >
                  {{ $t(`pages.servers.modes.${mode.key}.name`) }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.hosting.server_name") }}</Label>
            <Input
              v-model="buyLabel"
              maxlength="64"
              :placeholder="$t('pages.servers.host.name_placeholder')"
            />
          </div>

          <ol
            class="m-0 list-decimal space-y-1 rounded-lg border border-border bg-muted/20 py-2.5 pe-3 ps-7 text-[0.75rem] leading-relaxed text-muted-foreground"
          >
            <li>{{ $t("pages.servers.host.terms_1") }}</li>
            <li>{{ $t("pages.servers.host.terms_2") }}</li>
            <li>{{ $t("pages.hosting.terms_3") }}</li>
            <li>{{ $t("pages.store.terms_1") }}</li>
          </ol>

          <button
            type="button"
            class="flex w-full items-start gap-3 rounded-lg border px-3 py-3 text-start"
            :class="
              termsAccepted ? 'border-primary/50 bg-primary/5' : 'border-border'
            "
            @click="termsAccepted = !termsAccepted"
          >
            <Checkbox
              :model-value="termsAccepted"
              class="pointer-events-none mt-0.5"
              tabindex="-1"
            />
            <span class="text-sm leading-snug">
              {{ $t("pages.store.terms_accept") }}
            </span>
          </button>
        </div>

        <DialogFooter class="gap-2">
          <Button
            type="button"
            variant="ghost"
            :disabled="paying"
            @click="buyOpen = false"
          >
            {{ $t("common.cancel") }}
          </Button>
          <Button
            v-if="buyPlan?.price_ypoint"
            type="button"
            variant="outline"
            :disabled="paying || !termsAccepted || !canAffordPlan"
            :title="
              canAffordPlan ? undefined : $t('pages.store.ypoint_not_enough')
            "
            @click="pay('ypoint')"
          >
            <img
              src="/img/ypoint-logo.png"
              alt=""
              class="h-4 w-4 object-contain"
            />
            {{ $t("pages.store.pay_with_ypoint", { n: buyPlan.price_ypoint }) }}
          </Button>
          <Button
            type="button"
            :disabled="paying || !termsAccepted"
            @click="pay('bale')"
          >
            {{
              paying
                ? $t("pages.store.buying")
                : $t("pages.store.pay_with_bale")
            }}
          </Button>
        </DialogFooter>
        <p
          v-if="buyPlan?.price_ypoint && ypointBalance !== null"
          class="m-0 text-end text-xs text-muted-foreground"
        >
          {{ $t("pages.store.ypoint_balance_hint", { n: ypointBalance }) }}
        </p>
      </DialogScrollContent>
    </Dialog>

    <Dialog v-model:open="planEditorOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>
            {{
              editingPlanId
                ? $t("pages.servers.host.admin.edit")
                : $t("pages.servers.host.admin.add")
            }}
          </DialogTitle>
          <DialogDescription>
            {{ $t("pages.servers.host.admin.form_hint") }}
          </DialogDescription>
        </DialogHeader>

        <div class="space-y-3">
          <div class="space-y-1.5">
            <Label>{{ $t("pages.servers.host.admin.plan_title") }}</Label>
            <Input v-model="planForm.title" maxlength="80" />
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.servers.host.admin.plan_description") }}</Label>
            <Input v-model="planForm.description" maxlength="500" />
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.servers.host.admin.duration") }}</Label>
            <Select v-model="planForm.duration">
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem
                  v-for="d in DURATION_PRESETS"
                  :key="d"
                  :value="d"
                >
                  {{ formatHostedDuration(d, t) }}
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.admin.price_toman") }}</Label>
              <Input v-model.number="planForm.price_toman" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.admin.price_ypoint") }}</Label>
              <Input
                v-model.number="planForm.price_ypoint"
                type="number"
                min="0"
                :placeholder="$t('pages.servers.host.admin.ypoint_optional')"
              />
            </div>
          </div>
          <div class="space-y-1.5">
            <Label>{{ $t("pages.servers.host.admin.sort_order") }}</Label>
            <Input v-model.number="planForm.sort_order" type="number" />
          </div>
          <button
            type="button"
            class="flex w-full items-center justify-between gap-3 rounded-lg border border-border px-3 py-2.5 text-sm"
            @click="planForm.active = !planForm.active"
          >
            <span>{{ $t("pages.servers.host.admin.active") }}</span>
            <Checkbox
              :model-value="planForm.active"
              class="pointer-events-none"
              tabindex="-1"
            />
          </button>
        </div>

        <DialogFooter class="gap-2">
          <Button
            type="button"
            variant="ghost"
            :disabled="planSaving"
            @click="planEditorOpen = false"
          >
            {{ $t("common.cancel") }}
          </Button>
          <Button type="button" :disabled="planSaving" @click="savePlan">
            {{ $t("pages.servers.host.admin.save") }}
          </Button>
        </DialogFooter>
      </DialogScrollContent>
    </Dialog>
  </div>
</template>
