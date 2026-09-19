<script setup lang="ts">
import gql from "graphql-tag";
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import { Minus, Plus, ShoppingBag, Trash2 } from "lucide-vue-next";
import TacticalPageHeader from "~/components/TacticalPageHeader.vue";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Skeleton } from "~/components/ui/skeleton";
import Empty from "~/components/ui/empty/Empty.vue";
import { Checkbox } from "~/components/ui/checkbox";
import {
  Dialog,
  DialogScrollContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "~/components/ui/dialog";
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
  subscription_tier: string | null;
};

type CartLine = {
  product: Product;
  qty: number;
};

const products = ref<Product[]>([]);
const loading = ref(true);
const cart = ref<CartLine[]>([]);
const checkoutOpen = ref(false);
const termsAccepted = ref(false);
const paying = ref(false);
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

const PRODUCTS_QUERY = gql`
  query StoreProducts {
    store_products(
      where: { active: { _eq: true } }
      order_by: [{ price_irr: desc }, { sort_order: asc }]
    ) {
      id
      title
      slug
      description
      price_irr
      image_url
      ypoint_amount
      vip_duration
      subscription_tier
    }
  }
`;

const cartCount = computed(() =>
  cart.value.reduce((n, line) => n + line.qty, 0),
);
const cartTotalIrr = computed(() =>
  cart.value.reduce((sum, line) => sum + line.product.price_irr * line.qty, 0),
);

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

function qtyInCart(productId: string) {
  return cart.value.find((l) => l.product.id === productId)?.qty ?? 0;
}

function addToCart(product: Product) {
  const existing = cart.value.find((l) => l.product.id === product.id);
  if (existing) {
    if (existing.qty >= 10) {
      toast({
        variant: "destructive",
        title: t("pages.store.cart_limit"),
      });
      return;
    }
    existing.qty += 1;
  } else {
    cart.value.push({ product, qty: 1 });
  }
  toast({
    title: t("pages.store.added_to_cart"),
    description: product.title,
  });
}

function setQty(productId: string, qty: number) {
  const line = cart.value.find((l) => l.product.id === productId);
  if (!line) return;
  if (qty <= 0) {
    cart.value = cart.value.filter((l) => l.product.id !== productId);
    return;
  }
  line.qty = Math.min(10, qty);
}

function removeFromCart(productId: string) {
  cart.value = cart.value.filter((l) => l.product.id !== productId);
}

function openCheckout() {
  if (!cart.value.length) {
    toast({
      variant: "destructive",
      title: t("pages.store.cart_empty"),
    });
    return;
  }
  if (!useAuthStore().me?.steam_id) {
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: t("pages.store.sign_in_required"),
    });
    return;
  }
  termsAccepted.value = false;
  checkoutOpen.value = true;
}

/**
 * Same path that worked before: Hasura insert_store_orders_one → ble.ir deep link.
 * One product per Bale invoice (schema is single product_id). If the cart has
 * more than one unit, pay the first and leave the rest in the cart.
 */
async function payWithBale() {
  if (paying.value) return;
  if (!termsAccepted.value) {
    toast({
      variant: "destructive",
      title: t("pages.store.terms_required"),
    });
    return;
  }
  const steamId = useAuthStore().me?.steam_id;
  if (!steamId) {
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: t("pages.store.sign_in_required"),
    });
    return;
  }
  if (!cart.value.length) {
    toast({
      variant: "destructive",
      title: t("pages.store.cart_empty"),
    });
    return;
  }

  const line = cart.value[0];
  const product = line.product;
  paying.value = true;
  try {
    const { data: liveData } = await apollo.query({
      query: gql`
        query StoreProductPrice($id: uuid!) {
          store_products_by_pk(id: $id) {
            id
            price_irr
            active
          }
        }
      `,
      variables: { id: product.id },
      fetchPolicy: "network-only",
    });
    const live = liveData?.store_products_by_pk;
    if (!live?.active) {
      throw new Error("Product unavailable");
    }

    const orderId = crypto.randomUUID();
    const payload = `store:${orderId}`;
    await apollo.mutate({
      mutation: gql`
        mutation CreateStoreOrder(
          $id: uuid!
          $productId: uuid!
          $buyerSteamId: bigint!
          $amountIrr: Int!
          $payload: String!
        ) {
          insert_store_orders_one(
            object: {
              id: $id
              product_id: $productId
              buyer_steam_id: $buyerSteamId
              amount_irr: $amountIrr
              bale_payload: $payload
            }
          ) {
            id
          }
        }
      `,
      variables: {
        id: orderId,
        productId: product.id,
        buyerSteamId: steamId,
        amountIrr: live.price_irr,
        payload,
      },
    });

    void $fetch("/api/store/cancel-pending", {
      method: "POST",
      body: { exceptOrderId: orderId, steamId },
      credentials: "include",
    }).catch(() => undefined);

    const status = await $fetch<{ botUsername: string | null }>(
      "/api/store/status",
    );
    const start = `pay_${orderId.replace(/-/g, "")}`;
    const username = (status.botUsername || "yguardbot").replace(/^@/, "");
    const deepLink = `https://ble.ir/${username}?start=${start}`;

    // Consume one unit from cart; leave the rest for the next checkout.
    setQty(product.id, line.qty - 1);
    checkoutOpen.value = false;
    termsAccepted.value = false;

    toast({
      title: t("pages.store.checkout_started"),
      description: t("pages.store.checkout_hint"),
    });

    window.open(deepLink, "_blank", "noopener,noreferrer");
  } catch (error: any) {
    const message =
      error?.graphQLErrors?.[0]?.message ||
      error?.data?.message ||
      error?.data?.statusMessage ||
      error?.statusMessage ||
      error?.message ||
      String(error);
    toast({
      variant: "destructive",
      title: t("pages.store.checkout_failed"),
      description: Array.isArray(message) ? message.join(", ") : message,
    });
  } finally {
    paying.value = false;
  }
}

onMounted(() => {
  void refresh();
  void refreshYpoints();
});
</script>

<template>
  <div class="space-y-6 pb-24">
    <TacticalPageHeader>
      <template #title>{{ $t("pages.store.title") }}</template>
      <template #subtitle>{{ $t("pages.store.description") }}</template>
    </TacticalPageHeader>

    <p class="m-0 text-xs text-muted-foreground">
      {{ $t("pages.store.bale_rial_note") }}
    </p>

    <div
      v-if="ypointBalance !== null"
      class="inline-flex items-center gap-2 font-mono text-sm font-bold uppercase tracking-[0.12em] text-foreground"
    >
      <img
        src="/img/ypoint-logo.png"
        alt="Ypoint"
        class="h-6 w-6 shrink-0 object-contain"
      />
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
                  <span class="inline-flex items-center gap-1">
                    <img
                      src="/img/ypoint-logo.png"
                      alt=""
                      class="h-3.5 w-3.5 rounded-sm object-cover"
                    />
                    +{{ product.ypoint_amount }}
                  </span>
                </span>
                <span
                  v-if="product.subscription_tier"
                  class="font-mono text-[0.7rem] uppercase tracking-[0.12em] text-muted-foreground"
                >
                  {{
                    product.subscription_tier === "premium_plus"
                      ? $t("pages.challenges.tier_plus")
                      : $t("pages.challenges.tier_premium")
                  }}
                  · Challenges
                </span>
                <span
                  v-if="product.vip_duration"
                  class="font-mono text-[0.7rem] uppercase tracking-[0.12em] text-muted-foreground"
                >
                  {{ $t("pages.store.vip_badge", { duration: product.vip_duration }) }}
                </span>
              </div>
              <Button type="button" size="sm" @click="addToCart(product)">
                {{
                  qtyInCart(product.id)
                    ? $t("pages.store.add_again")
                    : $t("pages.store.add_to_cart")
                }}
              </Button>
            </div>
          </div>
        </article>
      </div>
    </PageTransition>

    <!-- Sticky cart bar -->
    <div
      v-if="cartCount > 0"
      class="fixed inset-x-0 bottom-0 z-40 border-t border-border bg-background/95 px-4 py-3 shadow-[0_-8px_24px_-12px_rgba(0,0,0,0.45)] backdrop-blur supports-[backdrop-filter]:bg-background/85"
    >
      <div class="mx-auto flex max-w-5xl items-center justify-between gap-3">
        <div class="min-w-0">
          <p
            class="m-0 font-mono text-[0.65rem] uppercase tracking-[0.14em] text-muted-foreground"
          >
            {{ $t("pages.store.cart_summary", { count: cartCount }) }}
          </p>
          <p class="m-0 font-mono text-sm font-semibold tabular-nums">
            {{ formatPrice(cartTotalIrr) }}
          </p>
        </div>
        <Button type="button" class="shrink-0" @click="openCheckout">
          {{ $t("pages.store.checkout") }}
        </Button>
      </div>
    </div>

    <Dialog v-model:open="checkoutOpen">
      <DialogScrollContent
        class="max-w-md gap-0 overflow-hidden p-0 sm:rounded-xl"
      >
        <div class="border-b border-border px-5 py-4">
          <DialogHeader class="space-y-1 text-left">
            <DialogTitle class="font-sans text-lg">
              {{ $t("pages.store.checkout_title") }}
            </DialogTitle>
            <DialogDescription class="text-xs">
              {{ $t("pages.store.checkout_subtitle") }}
            </DialogDescription>
          </DialogHeader>
        </div>

        <div class="max-h-[min(70vh,560px)] space-y-5 overflow-y-auto px-5 py-4">
          <!-- Cart lines -->
          <ul class="m-0 list-none space-y-2 p-0">
            <li
              v-for="line in cart"
              :key="line.product.id"
              class="flex items-center gap-3 rounded-lg border border-border/80 bg-card/50 p-2.5"
            >
              <div
                class="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted"
              >
                <img
                  v-if="line.product.image_url"
                  :src="line.product.image_url"
                  :alt="line.product.title"
                  class="h-full w-full object-cover"
                />
                <ShoppingBag v-else class="h-5 w-5 opacity-40" />
              </div>
              <div class="min-w-0 flex-1">
                <p class="m-0 truncate text-sm font-medium leading-tight">
                  {{ line.product.title }}
                </p>
                <p class="m-0 mt-0.5 font-mono text-xs text-muted-foreground">
                  {{ formatPrice(line.product.price_irr) }}
                  <span v-if="line.qty > 1">
                    × {{ line.qty }} =
                    {{ formatPrice(line.product.price_irr * line.qty) }}
                  </span>
                </p>
              </div>
              <div class="flex items-center gap-0.5">
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  class="h-7 w-7"
                  @click="setQty(line.product.id, line.qty - 1)"
                >
                  <Minus class="h-3.5 w-3.5" />
                </Button>
                <span
                  class="w-5 text-center font-mono text-sm tabular-nums"
                >
                  {{ line.qty }}
                </span>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  class="h-7 w-7"
                  @click="setQty(line.product.id, line.qty + 1)"
                >
                  <Plus class="h-3.5 w-3.5" />
                </Button>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  class="h-7 w-7 text-muted-foreground hover:text-destructive"
                  @click="removeFromCart(line.product.id)"
                >
                  <Trash2 class="h-3.5 w-3.5" />
                </Button>
              </div>
            </li>
          </ul>

          <div
            class="flex items-center justify-between rounded-lg border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.06)] px-3.5 py-2.5"
          >
            <span
              class="font-mono text-[0.7rem] uppercase tracking-[0.14em] text-muted-foreground"
            >
              {{ $t("pages.store.total") }}
            </span>
            <span class="font-mono text-base font-semibold tabular-nums">
              {{ formatPrice(cartTotalIrr) }}
            </span>
          </div>

          <!-- Terms -->
          <section class="space-y-2.5">
            <h3
              class="m-0 font-mono text-[0.7rem] font-semibold uppercase tracking-[0.14em] text-muted-foreground"
            >
              {{ $t("pages.store.terms_title") }}
            </h3>
            <ol
              class="m-0 max-h-36 list-decimal space-y-1.5 overflow-y-auto rounded-lg border border-border bg-muted/20 py-2.5 pe-3 ps-7 text-[0.75rem] leading-relaxed text-muted-foreground"
            >
              <li>{{ $t("pages.store.terms_1") }}</li>
              <li>{{ $t("pages.store.terms_2") }}</li>
              <li>{{ $t("pages.store.terms_3") }}</li>
              <li>{{ $t("pages.store.terms_4") }}</li>
              <li>{{ $t("pages.store.terms_5") }}</li>
            </ol>

            <button
              type="button"
              class="flex w-full items-start gap-3 rounded-lg border px-3 py-3 text-start transition-colors"
              :class="
                termsAccepted
                  ? 'border-[hsl(var(--tac-amber)/0.5)] bg-[hsl(var(--tac-amber)/0.08)]'
                  : 'border-border bg-card/40 hover:bg-muted/30'
              "
              @click="termsAccepted = !termsAccepted"
            >
              <Checkbox
                :model-value="termsAccepted"
                class="mt-0.5 pointer-events-none"
                tabindex="-1"
              />
              <span class="text-sm leading-snug text-foreground">
                {{ $t("pages.store.terms_accept") }}
              </span>
            </button>
          </section>
        </div>

        <DialogFooter
          class="flex-row gap-2 border-t border-border bg-background px-5 py-3.5 sm:justify-between"
        >
          <Button
            type="button"
            variant="ghost"
            class="px-3"
            :disabled="paying"
            @click="checkoutOpen = false"
          >
            {{ $t("common.cancel") }}
          </Button>
          <Button
            type="button"
            class="min-w-[10rem]"
            :disabled="paying || !termsAccepted || cartCount === 0"
            @click="payWithBale"
          >
            {{
              paying
                ? $t("pages.store.buying")
                : $t("pages.store.pay_with_bale")
            }}
          </Button>
        </DialogFooter>
      </DialogScrollContent>
    </Dialog>
  </div>
</template>
