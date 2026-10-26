<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Crown, ShoppingBag, Server } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Label } from "~/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import { toast } from "~/components/ui/toast";
import {
  hostedApi,
  hostedErrorMessage,
} from "~/composables/useHostedServers";
import { formatTomanAmount } from "~/utilities/irrToman";
import { useAuthStore } from "~/stores/AuthStore";

type Duration = "7d" | "30d" | "90d";
type Kind = "club_vip" | "public_vip" | "custom";

type Package = {
  duration: Duration;
  price_irr: number;
  price_ypoint: number | null;
};

type Product = {
  id: string;
  kind: Kind;
  title: string | null;
  description: string | null;
  enabled: boolean;
  ypoint_enabled: boolean;
  packages: Package[];
};

const props = defineProps<{
  clubId: string;
}>();

const { t, locale } = useI18n();
const auth = useAuthStore();
const loading = ref(true);
const products = ref<Product[]>([]);
const servers = ref<Array<{ id: string; label: string }>>([]);
const selected = ref<Record<string, Duration>>({});
const serverPick = ref("");
const terms = ref(false);
const busyId = ref<string | null>(null);

function productTitle(p: Product) {
  if (p.kind === "club_vip") return t("pages.clubs.shop.kind_club_vip");
  if (p.kind === "public_vip") return t("pages.clubs.shop.kind_public_vip");
  return p.title || t("pages.clubs.shop.kind_custom");
}

function productDesc(p: Product) {
  if (p.description?.trim()) return p.description;
  if (p.kind === "club_vip") return t("pages.clubs.shop.desc_club_vip");
  if (p.kind === "public_vip") return t("pages.clubs.shop.desc_public_vip");
  return "";
}

async function load() {
  loading.value = true;
  try {
    const data = await hostedApi<{
      products: Product[];
      servers: Array<{ id: string; label: string }>;
    }>(`/hosted-servers/clubs/${props.clubId}/shop`);
    products.value = data.products || [];
    servers.value = data.servers || [];
    for (const p of products.value) {
      if (!selected.value[p.id] && p.packages[0]) {
        selected.value[p.id] = p.packages[0].duration;
      }
    }
    if (!serverPick.value && servers.value[0]) {
      serverPick.value = servers.value[0].id;
    }
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

function pkgFor(p: Product) {
  const d = selected.value[p.id] || p.packages[0]?.duration;
  return p.packages.find((x) => x.duration === d) || p.packages[0] || null;
}

async function buy(p: Product, payWith: "bale" | "ypoint") {
  if (!auth.me?.steam_id) {
    toast({
      variant: "destructive",
      title: t("pages.store.sign_in_required"),
    });
    return;
  }
  if (!terms.value) {
    toast({
      variant: "destructive",
      title: t("pages.clubs.shop.terms_required"),
    });
    return;
  }
  const pkg = pkgFor(p);
  if (!pkg) return;
  if (p.kind === "public_vip" && !serverPick.value) {
    toast({
      variant: "destructive",
      title: t("pages.clubs.shop.pick_server"),
    });
    return;
  }
  if (payWith === "ypoint" && !(pkg.price_ypoint && pkg.price_ypoint > 0)) {
    return;
  }

  busyId.value = `${p.id}:${payWith}`;
  try {
    const result = await hostedApi<{
      paid?: boolean;
      deepLink?: string | null;
    }>(`/hosted-servers/clubs/${props.clubId}/shop/checkout`, {
      method: "POST",
      body: {
        product_id: p.id,
        duration: pkg.duration,
        payWith,
        server_id: p.kind === "public_vip" ? serverPick.value : null,
        termsAccepted: true,
      },
    });
    if (result?.deepLink) {
      toast({
        title: t("pages.store.checkout_started"),
        description: t("pages.store.checkout_hint"),
      });
      window.open(result.deepLink, "_blank", "noopener,noreferrer");
    } else {
      toast({ title: t("pages.clubs.shop.bought") });
      await load();
    }
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busyId.value = null;
  }
}

const empty = computed(() => !loading.value && products.value.length === 0);
</script>

<template>
  <div class="space-y-5">
    <div class="flex items-start gap-3">
      <div
        class="grid h-10 w-10 place-items-center rounded-xl bg-[rgba(255,85,0,0.12)] text-[#ff6a1f]"
      >
        <ShoppingBag class="h-5 w-5" />
      </div>
      <div>
        <h2 class="text-lg font-semibold tracking-tight">
          {{ t("pages.clubs.shop.title") }}
        </h2>
        <p class="mt-0.5 text-sm text-muted-foreground">
          {{ t("pages.clubs.shop.subtitle") }}
        </p>
      </div>
    </div>

    <div v-if="loading" class="space-y-3">
      <div
        v-for="i in 2"
        :key="i"
        class="h-36 animate-pulse rounded-2xl bg-card/60"
      />
    </div>

    <p
      v-else-if="empty"
      class="rounded-2xl border border-dashed border-border/80 px-4 py-10 text-center text-sm text-muted-foreground"
    >
      {{ t("pages.clubs.shop.empty") }}
    </p>

    <div v-else class="space-y-4">
      <div
        v-if="products.some((p) => p.kind === 'public_vip') && servers.length"
        class="rounded-xl border border-border/70 bg-card/40 p-3"
      >
        <Label class="mb-1.5 flex items-center gap-1.5 text-xs text-muted-foreground">
          <Server class="h-3.5 w-3.5" />
          {{ t("pages.clubs.shop.server_for_public_vip") }}
        </Label>
        <Select v-model="serverPick">
          <SelectTrigger class="h-9">
            <SelectValue :placeholder="t('pages.clubs.shop.pick_server')" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="s in servers" :key="s.id" :value="s.id">
              {{ s.label }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <article
        v-for="p in products"
        :key="p.id"
        class="rounded-2xl border border-border/70 bg-card/50 p-4 shadow-sm"
      >
        <div class="flex items-start gap-3">
          <Crown class="mt-0.5 h-5 w-5 shrink-0 text-[#ff6a1f]" />
          <div class="min-w-0 flex-1">
            <h3 class="font-semibold tracking-tight">{{ productTitle(p) }}</h3>
            <p
              v-if="productDesc(p)"
              class="mt-1 text-sm leading-relaxed text-muted-foreground"
            >
              {{ productDesc(p) }}
            </p>

            <div class="mt-3 flex flex-wrap gap-1.5">
              <button
                v-for="pkg in p.packages"
                :key="pkg.duration"
                type="button"
                class="rounded-lg px-2.5 py-1.5 text-xs font-medium transition"
                :class="
                  selected[p.id] === pkg.duration
                    ? 'bg-[#ff5500] text-white'
                    : 'bg-white/5 text-white/70 hover:bg-white/10'
                "
                @click="selected[p.id] = pkg.duration"
              >
                {{ t(`pages.clubs.shop.duration_${pkg.duration}`) }}
                ·
                {{ formatTomanAmount(pkg.price_irr, locale) }}
                {{ t("pages.clubs.shop.toman") }}
              </button>
            </div>

            <div class="mt-3 flex flex-wrap gap-2">
              <Button
                type="button"
                size="sm"
                class="bg-[#ff5500] hover:bg-[#ff6a1f]"
                :disabled="!!busyId || !pkgFor(p)"
                @click="buy(p, 'bale')"
              >
                {{
                  busyId === `${p.id}:bale`
                    ? t("common.loading")
                    : t("pages.clubs.shop.buy_irr")
                }}
              </Button>
              <Button
                v-if="pkgFor(p)?.price_ypoint"
                type="button"
                size="sm"
                variant="outline"
                :disabled="!!busyId"
                @click="buy(p, 'ypoint')"
              >
                {{
                  busyId === `${p.id}:ypoint`
                    ? t("common.loading")
                    : t("pages.clubs.shop.buy_ypoint", {
                        n: pkgFor(p)?.price_ypoint,
                      })
                }}
              </Button>
            </div>
          </div>
        </div>
      </article>

      <label class="flex items-start gap-2 text-sm text-muted-foreground">
        <Checkbox v-model:checked="terms" class="mt-0.5" />
        <span>{{ t("pages.clubs.shop.terms") }}</span>
      </label>
    </div>
  </div>
</template>
