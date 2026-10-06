<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Crown } from "lucide-vue-next";
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

const props = defineProps<{
  hostedId: string;
  shop: HostedVipShop | null | undefined;
}>();

const emit = defineEmits<{
  updated: [server: HostedServer];
}>();

const { t } = useI18n();
const busy = ref(false);
const enabled = ref(false);
// String models: `v-model.number` + our Input wrapper fights backspacing
// (empty → NaN → 0) so the fields feel frozen.
const price7d = ref("");
const price30d = ref("");
const price90d = ref("");
const editingPrices = ref(false);

function priceText(value: unknown) {
  const n = Math.max(0, Math.floor(Number(value) || 0));
  return String(n);
}

function applyShop(shop: HostedVipShop | null | undefined) {
  enabled.value = !!shop?.enabled;
  if (editingPrices.value) return;
  price7d.value = priceText(shop?.price_7d);
  price30d.value = priceText(shop?.price_30d);
  price90d.value = priceText(shop?.price_90d);
}

watch(
  () => [props.hostedId, props.shop] as const,
  () => applyShop(props.shop),
  { immediate: true, deep: true },
);

function parsePrice(raw: string) {
  return Math.max(0, Math.floor(Number(String(raw).replace(/[^\d]/g, "")) || 0));
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
          price_7d: parsePrice(price7d.value),
          price_30d: parsePrice(price30d.value),
          price_90d: parsePrice(price90d.value),
        },
      },
    );
    emit("updated", server);
    editingPrices.value = false;
    applyShop(server.vip_shop);
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

    <label class="flex items-center gap-2 text-sm">
      <Checkbox :checked="enabled" @update:checked="(v) => (enabled = !!v)" />
      {{ $t("pages.hosting.vip_shop.enabled") }}
    </label>

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
