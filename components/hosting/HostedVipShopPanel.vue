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
const price7d = ref(0);
const price30d = ref(0);
const price90d = ref(0);

function applyShop(shop: HostedVipShop | null | undefined) {
  enabled.value = !!shop?.enabled;
  price7d.value = Number(shop?.price_7d || 0);
  price30d.value = Number(shop?.price_30d || 0);
  price90d.value = Number(shop?.price_90d || 0);
}

watch(
  () => [props.hostedId, props.shop] as const,
  () => applyShop(props.shop),
  { immediate: true, deep: true },
);

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
          price_7d: Math.max(0, Math.floor(Number(price7d.value) || 0)),
          price_30d: Math.max(0, Math.floor(Number(price30d.value) || 0)),
          price_90d: Math.max(0, Math.floor(Number(price90d.value) || 0)),
        },
      },
    );
    emit("updated", server);
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
          v-model.number="price7d"
          type="number"
          min="0"
          step="1"
          dir="ltr"
          :disabled="!enabled"
        />
      </div>
      <div class="space-y-1.5">
        <Label>{{ $t("pages.hosting.vip_shop.price_30d") }}</Label>
        <Input
          v-model.number="price30d"
          type="number"
          min="0"
          step="1"
          dir="ltr"
          :disabled="!enabled"
        />
      </div>
      <div class="space-y-1.5">
        <Label>{{ $t("pages.hosting.vip_shop.price_90d") }}</Label>
        <Input
          v-model.number="price90d"
          type="number"
          min="0"
          step="1"
          dir="ltr"
          :disabled="!enabled"
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
