<script setup lang="ts">
import gql from "graphql-tag";
import { onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import { ShoppingBag } from "lucide-vue-next";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Skeleton } from "~/components/ui/skeleton";
import Empty from "~/components/ui/empty/Empty.vue";
import { toast } from "~/components/ui/toast";
import { formatTomanAmount } from "~/utilities/irrToman";

const { t, locale } = useI18n();
const { client: apollo } = useApolloClient();

type Product = {
  id: string;
  title: string;
  slug: string;
  description: string;
  price_irr: number;
  image_url: string | null;
  ypoint_amount: number | null;
  vip_duration: string | null;
};

const products = ref<Product[]>([]);
const loading = ref(true);
const buyingId = ref<string | null>(null);
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

const PRODUCTS_QUERY = gql`
  query StoreProducts {
    store_products(
      where: { active: { _eq: true } }
      order_by: [{ sort_order: asc }, { created_at: desc }]
    ) {
      id
      title
      slug
      description
      price_irr
      image_url
      ypoint_amount
      vip_duration
    }
  }
`;

async function refresh() {
  loading.value = true;
  try {
    const { data } = await apollo.query({
      query: PRODUCTS_QUERY,
      fetchPolicy: "network-only",
    });
    products.value = data?.store_products ?? [];
  } catch (error) {
    console.error(error);
    products.value = [];
  } finally {
    loading.value = false;
  }
}

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

async function buy(product: Product) {
  if (buyingId.value) return;
  const steamId = useAuthStore().me?.steam_id;
  if (!steamId) {
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: "Sign in required",
    });
    return;
  }
  buyingId.value = product.id;
  try {
    // Price is taken from DB on the API — never from a stale client cache.
    const checkout = await $fetch<{
      orderId: string;
      deepLink: string;
      botUsername: string | null;
      startParam: string;
    }>("/api/store/checkout", {
      method: "POST",
      body: { productId: product.id },
      credentials: "include",
    });

    const deepLink =
      checkout.deepLink ||
      (() => {
        const username = (checkout.botUsername || "yguardbot").replace(
          /^@/,
          "",
        );
        return `https://ble.ir/${username}?start=${checkout.startParam}`;
      })();

    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.store.checkout_hint"),
    });

    window.open(deepLink, "_blank", "noopener,noreferrer");
  } catch (error: any) {
    const message =
      error?.graphQLErrors?.[0]?.message ||
      error?.data?.message ||
      error?.statusMessage ||
      error?.message ||
      String(error);
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: message,
    });
  } finally {
    buyingId.value = null;
  }
}

onMounted(() => {
  void refresh();
  void refreshYpoints();
});
</script>

<template>
  <div class="space-y-6">
    <TacticalPageHeader>
      <template #title>{{ $t("pages.store.title") }}</template>
      <template #subtitle>{{ $t("pages.store.description") }}</template>
    </TacticalPageHeader>

    <p class="m-0 text-xs text-muted-foreground">
      {{ $t("pages.store.bale_rial_note") }}
    </p>

    <div
      v-if="ypointBalance !== null"
      class="inline-flex items-center gap-2 border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.08)] px-3 py-2 font-mono text-sm font-bold uppercase tracking-[0.12em] text-[hsl(var(--tac-amber))]"
    >
      <span class="opacity-70">{{ $t("ypoint.balance_label") }}</span>
      <span class="tabular-nums">{{ ypointBalance }}</span>
    </div>

    <PageTransition>
      <div v-if="loading" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton v-for="i in 6" :key="i" class="h-64 rounded-lg" />
      </div>

      <Empty v-else-if="products.length === 0">
        <h2 class="m-0 text-lg font-semibold">{{ $t("pages.store.empty_title") }}</h2>
        <p class="m-0 text-sm text-muted-foreground">
          {{ $t("pages.store.empty_body") }}
        </p>
      </Empty>

      <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <article
          v-for="product in products"
          :key="product.id"
          class="flex flex-col overflow-hidden rounded-lg border border-border bg-card/40"
        >
          <div class="aspect-[16/10] bg-muted">
            <img
              v-if="product.image_url"
              :src="product.image_url"
              :alt="product.title"
              class="h-full w-full object-cover"
            />
            <div
              v-else
              class="flex h-full w-full items-center justify-center text-muted-foreground"
            >
              <ShoppingBag class="h-10 w-10 opacity-40" />
            </div>
          </div>
          <div class="flex flex-1 flex-col gap-3 p-4">
            <div>
              <h2 class="m-0 font-sans text-base font-semibold">
                {{ product.title }}
              </h2>
              <p
                v-if="product.description"
                class="mt-1 line-clamp-3 text-sm text-muted-foreground"
              >
                {{ product.description }}
              </p>
            </div>
            <div class="mt-auto flex items-center justify-between gap-3">
              <div class="flex flex-col gap-0.5">
                <span class="font-mono text-sm font-semibold tabular-nums">
                  {{ formatPrice(product.price_irr) }}
                </span>
                <span
                  v-if="product.ypoint_amount"
                  class="font-mono text-[0.7rem] uppercase tracking-[0.12em] text-[hsl(var(--tac-amber))]"
                >
                  +{{ product.ypoint_amount }} YP
                </span>
                <span
                  v-if="product.vip_duration"
                  class="font-mono text-[0.7rem] uppercase tracking-[0.12em] text-muted-foreground"
                >
                  {{ $t("pages.store.vip_badge", { duration: product.vip_duration }) }}
                </span>
              </div>
              <Button
                type="button"
                size="sm"
                :disabled="buyingId === product.id"
                @click="buy(product)"
              >
                {{
                  buyingId === product.id
                    ? $t("pages.store.buying")
                    : $t("pages.store.buy")
                }}
              </Button>
            </div>
          </div>
        </article>
      </div>
    </PageTransition>
  </div>
</template>
