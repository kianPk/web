<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { ShoppingBag } from "lucide-vue-next";
import {
  hostedApi,
  hostedErrorMessage,
} from "~/composables/useHostedServers";
import { formatTomanAmount } from "~/utilities/irrToman";
import { formatSiteDate } from "~/utilities/dateLocale";
import { toast } from "~/components/ui/toast";

type Purchase = {
  id: string;
  club_id: string;
  club_name: string;
  club_slug: string;
  kind: string;
  product_title: string;
  duration: string | null;
  amount_irr: number;
  amount_ypoint: number;
  payment_method: string;
  status: string;
  fulfillment_status: string;
  store_order_id?: string | null;
  buyer_steam_id?: string | null;
  paid_at?: string | null;
  created_at: string;
};

const loading = ref(true);
const rows = ref<Purchase[]>([]);
const { t, locale } = useI18n();

function statusLabel(row: Purchase) {
  if (row.kind === "custom" && row.fulfillment_status === "pending") {
    return t("pages.clubs.shop.status_awaiting_fulfillment");
  }
  if (row.kind === "custom" && row.fulfillment_status === "done") {
    return t("pages.clubs.shop.status_fulfilled");
  }
  return t("pages.clubs.shop.status_paid");
}

function invoiceId(row: Purchase) {
  return row.store_order_id || row.id;
}

function purchaseAt(row: Purchase) {
  return formatSiteDate(row.paid_at || row.created_at, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

async function load() {
  loading.value = true;
  try {
    rows.value = await hostedApi<Purchase[]>(
      `/hosted-servers/clubs/shop/my-purchases`,
    );
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

onMounted(() => void load());
</script>

<template>
  <section class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
    <div class="flex items-center gap-2">
      <ShoppingBag class="h-4 w-4 text-[#ff6a1f]" />
      <h3 class="text-sm font-semibold">
        {{ t("pages.clubs.shop.my_purchases") }}
      </h3>
    </div>
    <p class="text-xs text-muted-foreground">
      {{ t("pages.clubs.shop.my_purchases_hint") }}
    </p>

    <div v-if="loading" class="h-16 animate-pulse rounded-lg bg-background/50" />
    <p
      v-else-if="!rows.length"
      class="rounded-lg border border-dashed border-border/60 px-3 py-4 text-center text-xs text-muted-foreground"
    >
      {{ t("pages.clubs.shop.my_purchases_empty") }}
    </p>
    <ul v-else class="space-y-2">
      <li
        v-for="row in rows"
        :key="row.id"
        class="space-y-1.5 rounded-lg border border-border/50 bg-background/30 px-3 py-2.5 text-sm"
      >
        <div class="flex flex-wrap items-center gap-2">
          <div class="min-w-0 flex-1">
            <NuxtLink
              :to="`/clubs/${row.club_slug}`"
              class="font-medium text-foreground hover:text-[#ff6a1f]"
            >
              {{ row.club_name }}
            </NuxtLink>
            <div class="truncate text-xs text-muted-foreground">
              {{ row.product_title }}
              <template v-if="row.duration"> · {{ row.duration }}</template>
            </div>
          </div>
          <span class="text-xs text-muted-foreground">
            <template v-if="row.payment_method === 'ypoint'">
              {{ row.amount_ypoint }} YP
            </template>
            <template v-else>
              {{ formatTomanAmount(row.amount_irr, locale) }}
              {{ t("pages.clubs.shop.toman") }}
            </template>
          </span>
          <span
            class="rounded-full border border-border/60 px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground"
          >
            {{ statusLabel(row) }}
          </span>
        </div>
        <dl class="grid gap-1 text-[11px] text-muted-foreground sm:grid-cols-3">
          <div class="min-w-0">
            <dt class="inline text-muted-foreground/80">
              {{ t("pages.clubs.shop.history_invoice") }}:
            </dt>
            {{ " " }}
            <dd class="inline font-mono text-foreground/80" dir="ltr">
              {{ invoiceId(row) }}
            </dd>
          </div>
          <div v-if="row.buyer_steam_id" class="min-w-0">
            <dt class="inline text-muted-foreground/80">
              {{ t("pages.clubs.shop.history_buyer") }}:
            </dt>
            {{ " " }}
            <dd class="inline font-mono text-foreground/80" dir="ltr">
              {{ row.buyer_steam_id }}
            </dd>
          </div>
          <div class="min-w-0">
            <dt class="inline text-muted-foreground/80">
              {{ t("pages.clubs.shop.history_paid_at") }}:
            </dt>
            {{ " " }}
            <dd class="inline text-foreground/80">{{ purchaseAt(row) }}</dd>
          </div>
        </dl>
      </li>
    </ul>
  </section>
</template>
