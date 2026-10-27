<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Plus, Save, Trash2 } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { toast } from "~/components/ui/toast";
import {
  hostedApi,
  hostedErrorMessage,
} from "~/composables/useHostedServers";
import {
  formatTomanAmount,
  irrToToman,
  parseTomanInput,
  tomanToIrr,
} from "~/utilities/irrToman";

type Kind = "club_vip" | "public_vip" | "custom";

type Product = {
  id: string;
  kind: Kind;
  title: string | null;
  description: string | null;
  enabled: boolean;
  ypoint_enabled: boolean;
  price_7d_irr: number;
  price_30d_irr: number;
  price_90d_irr: number;
  price_7d_ypoint: number;
  price_30d_ypoint: number;
  price_90d_ypoint: number;
};

type Draft = Product & {
  price_7d_toman: string;
  price_30d_toman: string;
  price_90d_toman: string;
  price_7d_yp: string;
  price_30d_yp: string;
  price_90d_yp: string;
};

type ShopOrder = {
  id: string;
  kind: Kind;
  product_title: string;
  buyer_steam_id: string;
  buyer_name?: string | null;
  amount_irr: number;
  amount_ypoint: number;
  payment_method: string;
  fulfillment_status: string;
  status: string;
  paid_at?: string | null;
};

const props = defineProps<{
  clubId: string;
}>();

const { t, locale } = useI18n();
const loading = ref(true);
const saving = ref(false);
const drafts = ref<Draft[]>([]);
const customCount = ref(0);
const orders = ref<ShopOrder[]>([]);

function asBool(value: unknown) {
  return value === true || value === 1 || value === "1" || value === "t" || value === "true";
}

function toDraft(p: Product): Draft {
  return {
    ...p,
    enabled: asBool(p.enabled),
    ypoint_enabled: asBool(p.ypoint_enabled),
    title: p.title ?? "",
    description: p.description ?? "",
    price_7d_toman: String(irrToToman(p.price_7d_irr || 0)),
    price_30d_toman: String(irrToToman(p.price_30d_irr || 0)),
    price_90d_toman: String(irrToToman(p.price_90d_irr || 0)),
    price_7d_yp: String(p.price_7d_ypoint || 0),
    price_30d_yp: String(p.price_30d_ypoint || 0),
    price_90d_yp: String(p.price_90d_ypoint || 0),
  };
}

function toggleFlag(d: Draft, key: "enabled" | "ypoint_enabled") {
  d[key] = !asBool(d[key]);
}

function kindLabel(kind: Kind) {
  if (kind === "club_vip") return t("pages.clubs.shop.kind_club_vip");
  if (kind === "public_vip") return t("pages.clubs.shop.kind_public_vip");
  return t("pages.clubs.shop.kind_custom");
}

async function load() {
  loading.value = true;
  try {
    const [shop, orderRows] = await Promise.all([
      hostedApi<{
        products: Product[];
        custom_count: number;
      }>(`/hosted-servers/clubs/${props.clubId}/shop?manage=1`),
      hostedApi<ShopOrder[]>(
        `/hosted-servers/clubs/${props.clubId}/shop/orders`,
      ).catch(() => []),
    ]);
    drafts.value = (shop.products || []).map(toDraft);
    customCount.value = shop.custom_count || 0;
    orders.value = orderRows || [];
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.clubId,
  () => void load(),
);
onMounted(() => void load());

function addCustom() {
  if (customCount.value >= 2) {
    toast({
      variant: "destructive",
      title: t("pages.clubs.shop.custom_limit"),
    });
    return;
  }
  drafts.value.push(
    toDraft({
      id: "",
      kind: "custom",
      title: "",
      description: "",
      enabled: false,
      ypoint_enabled: false,
      price_7d_irr: 0,
      price_30d_irr: 0,
      price_90d_irr: 0,
      price_7d_ypoint: 0,
      price_30d_ypoint: 0,
      price_90d_ypoint: 0,
    }),
  );
  customCount.value += 1;
}

async function removeCustom(d: Draft, index: number) {
  if (d.kind !== "custom") return;
  if (!d.id) {
    drafts.value.splice(index, 1);
    customCount.value = Math.max(0, customCount.value - 1);
    return;
  }
  saving.value = true;
  try {
    await hostedApi(`/hosted-servers/clubs/${props.clubId}/shop`, {
      method: "POST",
      body: { products: [{ id: d.id, kind: "custom", delete: true }] },
    });
    toast({ title: t("pages.clubs.shop.saved") });
    await load();
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    saving.value = false;
  }
}

async function saveAll() {
  saving.value = true;
  try {
    const products = drafts.value.map((d) => ({
      id: d.id || undefined,
      kind: d.kind,
      title: d.title,
      description: d.description,
      enabled: asBool(d.enabled),
      ypoint_enabled: asBool(d.ypoint_enabled),
      price_7d_irr: tomanToIrr(parseTomanInput(d.price_7d_toman)),
      price_30d_irr: tomanToIrr(parseTomanInput(d.price_30d_toman)),
      price_90d_irr: tomanToIrr(parseTomanInput(d.price_90d_toman)),
      price_7d_ypoint: parseTomanInput(d.price_7d_yp),
      price_30d_ypoint: parseTomanInput(d.price_30d_yp),
      price_90d_ypoint: parseTomanInput(d.price_90d_yp),
    }));
    await hostedApi(`/hosted-servers/clubs/${props.clubId}/shop`, {
      method: "POST",
      body: { products },
    });
    toast({ title: t("pages.clubs.shop.saved") });
    await load();
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    saving.value = false;
  }
}

async function markDone(orderId: string) {
  try {
    await hostedApi(
      `/hosted-servers/clubs/${props.clubId}/shop/orders/${orderId}`,
      {
        method: "PATCH",
        body: { fulfillment_status: "done" },
      },
    );
    toast({ title: t("pages.clubs.shop.order_done") });
    await load();
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  }
}

const pendingCustom = computed(() =>
  orders.value.filter(
    (o) =>
      o.kind === "custom" &&
      o.status === "paid" &&
      o.fulfillment_status === "pending",
  ),
);

const salesHistory = computed(() =>
  orders.value.filter((o) => o.status === "paid" || o.status === "pending"),
);

function saleStatus(o: ShopOrder) {
  if (o.status === "pending") return t("pages.clubs.shop.status_pending");
  if (o.kind === "custom" && o.fulfillment_status === "pending") {
    return t("pages.clubs.shop.status_awaiting_fulfillment");
  }
  if (o.kind === "custom" && o.fulfillment_status === "done") {
    return t("pages.clubs.shop.status_fulfilled");
  }
  return t("pages.clubs.shop.status_paid");
}
</script>

<template>
  <div class="space-y-5 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2">
    <div class="flex flex-wrap items-center justify-between gap-2">
      <div>
        <h3 class="text-sm font-semibold">{{ t("pages.clubs.shop.manage_title") }}</h3>
        <p class="mt-0.5 text-xs text-muted-foreground">
          {{ t("pages.clubs.shop.manage_hint") }}
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <Button
          type="button"
          size="sm"
          variant="outline"
          :disabled="saving || customCount >= 2"
          @click="addCustom"
        >
          <Plus class="me-1 h-3.5 w-3.5" />
          {{ t("pages.clubs.shop.add_custom") }}
        </Button>
        <Button type="button" size="sm" :disabled="saving || loading" @click="saveAll">
          <Save class="me-1 h-3.5 w-3.5" />
          {{ t("pages.clubs.shop.save") }}
        </Button>
      </div>
    </div>

    <p class="text-xs text-muted-foreground">
      {{ t("pages.clubs.shop.limit_hint", { custom: customCount, max: 2 }) }}
    </p>

    <div v-if="loading" class="h-24 animate-pulse rounded-xl bg-background/50" />

    <div v-else class="space-y-4">
      <article
        v-for="(d, index) in drafts"
        :key="d.id || `new-${index}`"
        class="space-y-3 rounded-xl border border-border/60 bg-background/40 p-3"
      >
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div class="font-medium">{{ kindLabel(d.kind) }}</div>
          <div class="flex items-center gap-3">
            <button
              type="button"
              class="flex items-center gap-1.5 text-xs"
              :disabled="saving"
              @click="toggleFlag(d, 'enabled')"
            >
              <Checkbox
                :checked="asBool(d.enabled)"
                class="pointer-events-none"
                tabindex="-1"
              />
              {{ t("pages.clubs.shop.enabled") }}
            </button>
            <button
              type="button"
              class="flex items-center gap-1.5 text-xs"
              :disabled="saving"
              @click="toggleFlag(d, 'ypoint_enabled')"
            >
              <Checkbox
                :checked="asBool(d.ypoint_enabled)"
                class="pointer-events-none"
                tabindex="-1"
              />
              {{ t("pages.clubs.shop.ypoint_enabled") }}
            </button>
            <Button
              v-if="d.kind === 'custom'"
              type="button"
              size="sm"
              variant="ghost"
              class="text-destructive"
              @click="removeCustom(d, index)"
            >
              <Trash2 class="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>

        <div v-if="d.kind === 'custom'" class="grid gap-2 sm:grid-cols-2">
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.custom_title") }}
            <Input v-model="d.title" class="h-9" maxlength="80" />
          </label>
          <label class="space-y-1 text-xs sm:col-span-2">
            {{ t("pages.clubs.shop.custom_desc") }}
            <Input v-model="d.description" class="h-9" maxlength="500" />
          </label>
        </div>
        <p v-else class="text-xs text-muted-foreground">
          {{
            d.kind === "club_vip"
              ? t("pages.clubs.shop.desc_club_vip")
              : t("pages.clubs.shop.desc_public_vip")
          }}
        </p>

        <div class="grid gap-2 sm:grid-cols-3">
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.price_7d") }}
            <Input v-model="d.price_7d_toman" class="h-9" inputmode="numeric" />
          </label>
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.price_30d") }}
            <Input v-model="d.price_30d_toman" class="h-9" inputmode="numeric" />
          </label>
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.price_90d") }}
            <Input v-model="d.price_90d_toman" class="h-9" inputmode="numeric" />
          </label>
        </div>

        <div v-if="d.ypoint_enabled" class="grid gap-2 sm:grid-cols-3">
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.yp_7d") }}
            <Input v-model="d.price_7d_yp" class="h-9" inputmode="numeric" />
          </label>
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.yp_30d") }}
            <Input v-model="d.price_30d_yp" class="h-9" inputmode="numeric" />
          </label>
          <label class="space-y-1 text-xs">
            {{ t("pages.clubs.shop.yp_90d") }}
            <Input v-model="d.price_90d_yp" class="h-9" inputmode="numeric" />
          </label>
        </div>
      </article>
    </div>

    <div v-if="pendingCustom.length" class="space-y-2 border-t border-border/60 pt-4">
      <h4 class="text-sm font-semibold">
        {{ t("pages.clubs.shop.pending_orders") }}
      </h4>
      <div
        v-for="o in pendingCustom"
        :key="o.id"
        class="flex flex-wrap items-center gap-2 rounded-lg border border-border/50 bg-background/30 px-3 py-2 text-sm"
      >
        <span class="min-w-0 flex-1 truncate font-medium">
          {{ o.product_title }} · {{ o.buyer_name || o.buyer_steam_id }}
        </span>
        <span class="text-xs text-muted-foreground">
          <template v-if="o.payment_method === 'ypoint'">
            {{ o.amount_ypoint }} YP
          </template>
          <template v-else>
            {{ formatTomanAmount(o.amount_irr, locale) }}
            {{ t("pages.clubs.shop.toman") }}
          </template>
        </span>
        <Button type="button" size="sm" @click="markDone(o.id)">
          {{ t("pages.clubs.shop.mark_done") }}
        </Button>
      </div>
    </div>

    <div class="space-y-2 border-t border-border/60 pt-4">
      <h4 class="text-sm font-semibold">
        {{ t("pages.clubs.shop.sales_history") }}
      </h4>
      <p class="text-xs text-muted-foreground">
        {{ t("pages.clubs.shop.sales_history_hint") }}
      </p>
      <p
        v-if="!loading && !salesHistory.length"
        class="rounded-lg border border-dashed border-border/60 px-3 py-4 text-center text-xs text-muted-foreground"
      >
        {{ t("pages.clubs.shop.sales_history_empty") }}
      </p>
      <div
        v-for="o in salesHistory"
        :key="`sale-${o.id}`"
        class="flex flex-wrap items-center gap-2 rounded-lg border border-border/50 bg-background/30 px-3 py-2 text-sm"
      >
        <span class="min-w-0 flex-1 truncate font-medium">
          {{ o.product_title }} · {{ o.buyer_name || o.buyer_steam_id }}
        </span>
        <span class="text-xs text-muted-foreground">
          <template v-if="o.payment_method === 'ypoint'">
            {{ o.amount_ypoint }} YP
          </template>
          <template v-else>
            {{ formatTomanAmount(o.amount_irr, locale) }}
            {{ t("pages.clubs.shop.toman") }}
          </template>
        </span>
        <span
          class="rounded-full border border-border/60 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground"
        >
          {{ saleStatus(o) }}
        </span>
      </div>
    </div>
  </div>
</template>
