<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Crown, Wallet } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { toast } from "~/components/ui/toast";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedServer,
  type HostedVipShop,
} from "~/composables/useHostedServers";
import { irrToToman, tomanToIrr, formatTomanAmount } from "~/utilities/irrToman";

const props = defineProps<{
  hostedId: string;
  shop: HostedVipShop | null | undefined;
  /** Owner wallet in Rials (preferred over /irr/me). */
  walletIrr?: number | null;
}>();

const emit = defineEmits<{
  updated: [server: HostedServer];
}>();

const { t, locale } = useI18n();
const busy = ref(false);
const enabled = ref(false);
// String models: `v-model.number` + our Input wrapper fights backspacing
// (empty → NaN → 0) so the fields feel frozen. Values are Tomans in the UI.
const price7d = ref("");
const price30d = ref("");
const price90d = ref("");
const editingPrices = ref(false);

const { balance: irrBalance, refresh: refreshIrr } = useIrrWallet();
void refreshIrr();

const displayWalletIrr = computed(() => {
  if (typeof props.walletIrr === "number" && Number.isFinite(props.walletIrr)) {
    return props.walletIrr;
  }
  if (
    typeof props.shop?.wallet_irr === "number" &&
    Number.isFinite(props.shop.wallet_irr)
  ) {
    return props.shop.wallet_irr;
  }
  return irrBalance.value;
});

function priceTextFromIrr(value: unknown) {
  return String(irrToToman(Number(value) || 0));
}

function applyShop(shop: HostedVipShop | null | undefined) {
  enabled.value = !!shop?.enabled;
  if (editingPrices.value) return;
  price7d.value = priceTextFromIrr(shop?.price_7d);
  price30d.value = priceTextFromIrr(shop?.price_30d);
  price90d.value = priceTextFromIrr(shop?.price_90d);
}

watch(
  () => [props.hostedId, props.shop] as const,
  () => applyShop(props.shop),
  { immediate: true, deep: true },
);

function parseToman(raw: string) {
  return Math.max(0, Math.floor(Number(String(raw).replace(/[^\d]/g, "")) || 0));
}

function toggleEnabled() {
  enabled.value = !enabled.value;
}

async function save() {
  if (busy.value) return;
  busy.value = true;
  try {
    const server = await hostedApi<HostedServer>(
      `/hosted-servers/${props.hostedId}/vip-shop`,
      {
        method: "POST",
        body: {
          enabled: enabled.value,
          price_7d: tomanToIrr(parseToman(price7d.value)),
          price_30d: tomanToIrr(parseToman(price30d.value)),
          price_90d: tomanToIrr(parseToman(price90d.value)),
        },
      },
    );
    emit("updated", server);
    editingPrices.value = false;
    applyShop(server.vip_shop);
    void refreshIrr();
    toast({ title: t("pages.hosting.vip_shop.saved") });
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <section
    class="space-y-4 rounded-lg border border-border bg-card/40 p-4 lg:col-span-2"
  >
    <div class="space-y-1">
      <h2 class="m-0 flex items-center gap-2 text-base font-semibold">
        <Crown class="h-4 w-4" />
        {{ $t("pages.hosting.vip_shop.title") }}
      </h2>
      <p class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.vip_shop.description") }}
      </p>
    </div>

    <div
      class="flex items-center justify-between gap-3 rounded-md border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.08)] px-3 py-2.5"
    >
      <div class="flex min-w-0 items-center gap-2 text-sm">
        <Wallet class="h-4 w-4 shrink-0 text-[hsl(var(--tac-amber))]" />
        <span class="text-muted-foreground">
          {{ $t("pages.hosting.vip_shop.wallet_label") }}
        </span>
      </div>
      <span
        class="shrink-0 font-mono text-sm font-semibold tabular-nums text-foreground"
        dir="ltr"
      >
        <template v-if="displayWalletIrr === null">…</template>
        <template v-else>
          {{ formatTomanAmount(displayWalletIrr, locale) }}
          {{ $t("pages.hosting.vip_shop.toman_unit") }}
        </template>
      </span>
    </div>

    <button
      type="button"
      class="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-start text-sm"
      :disabled="busy"
      @click="toggleEnabled"
    >
      <Checkbox
        :model-value="enabled"
        class="pointer-events-none"
        tabindex="-1"
      />
      {{ $t("pages.hosting.vip_shop.enabled") }}
    </button>

    <div class="grid gap-3 sm:grid-cols-3">
      <div class="space-y-1.5">
        <Label>{{ $t("pages.hosting.vip_shop.price_7d") }}</Label>
        <Input
          v-model="price7d"
          type="text"
          inputmode="numeric"
          pattern="[0-9]*"
          dir="ltr"
          :disabled="busy"
          @focus="editingPrices = true"
          @blur="editingPrices = false"
        />
      </div>
      <div class="space-y-1.5">
        <Label>{{ $t("pages.hosting.vip_shop.price_30d") }}</Label>
        <Input
          v-model="price30d"
          type="text"
          inputmode="numeric"
          pattern="[0-9]*"
          dir="ltr"
          :disabled="busy"
          @focus="editingPrices = true"
          @blur="editingPrices = false"
        />
      </div>
      <div class="space-y-1.5">
        <Label>{{ $t("pages.hosting.vip_shop.price_90d") }}</Label>
        <Input
          v-model="price90d"
          type="text"
          inputmode="numeric"
          pattern="[0-9]*"
          dir="ltr"
          :disabled="busy"
          @focus="editingPrices = true"
          @blur="editingPrices = false"
        />
      </div>
    </div>

    <p class="m-0 text-xs text-muted-foreground">
      {{ $t("pages.hosting.vip_shop.zero_hint") }}
    </p>

    <Button size="sm" :disabled="busy" @click="save">
      {{ $t("pages.hosting.vip_shop.save") }}
    </Button>
  </section>
</template>
