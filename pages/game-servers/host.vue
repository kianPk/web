<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { ArrowLeft, Clock, Server, Settings2 } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Skeleton } from "~/components/ui/skeleton";
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
  type HostedServer,
  type SectionHostOverview,
  type SectionHostQuote,
} from "~/composables/useHostedServers";
import { useAuthStore } from "~/stores/AuthStore";

const { t, locale } = useI18n();
const auth = useAuthStore();
const router = useRouter();
const { balance: ypointBalance, refresh: refreshYpoints } = useYpoints();

useHead(() => ({ title: t("pages.servers.host.title") }));

const overview = ref<SectionHostOverview | null>(null);
const quote = ref<SectionHostQuote | null>(null);
const myServers = ref<HostedServer[]>([]);
const loading = ref(true);
const quoting = ref(false);

const mode = ref<ServerModeKey>("duels");
const duration = ref("1d");
const label = ref("");
const termsAccepted = ref(false);
const paying = ref(false);
const checkoutOpen = ref(false);

const rateToman = ref(0);
const rateYpoint = ref(0);
const rateSaving = ref(false);

const signedIn = computed(() => Boolean(auth.me?.steam_id));
const isAdmin = computed(() => auth.isAdmin);

const modeOptions = computed(() => {
  const allowed = new Set(
    overview.value?.modes || SERVER_MODES.map((m) => m.key),
  );
  return SERVER_MODES.filter((m) => allowed.has(m.key));
});

const durationOptions = computed(
  () => overview.value?.durations || ["1h", "3h", "12h", "1d", "3d", "7d", "14d", "30d"],
);

const selectedMode = computed(
  () => modeOptions.value.find((m) => m.key === mode.value) || modeOptions.value[0],
);

const myHostServers = computed(() =>
  myServers.value.filter((s) => Boolean(s.section_mode)),
);

const canAffordYpoint = computed(() => {
  const price = Number(quote.value?.price_ypoint || 0);
  return price > 0 && (ypointBalance.value ?? 0) >= price;
});

function formatPrice(irr: number) {
  const numberLocale = locale.value?.startsWith("fa") ? "fa-IR" : "en-US";
  return t("pages.store.price", {
    amount: formatTomanAmount(irr, numberLocale),
  });
}

async function refreshOverview() {
  overview.value = await hostedApi<SectionHostOverview>(
    "/hosted-servers/section-host/overview",
  );
  if (overview.value.durations?.length && !overview.value.durations.includes(duration.value)) {
    duration.value = overview.value.durations[0];
  }
  if (overview.value.modes?.length && !overview.value.modes.includes(mode.value)) {
    mode.value = overview.value.modes[0] as ServerModeKey;
  }
  rateToman.value = irrToToman(overview.value.price_irr_per_hour || 0);
  rateYpoint.value = overview.value.price_ypoint_per_hour || 0;
}

async function refreshQuote() {
  quoting.value = true;
  try {
    quote.value = await hostedApi<SectionHostQuote>(
      `/hosted-servers/section-host/quote?duration=${encodeURIComponent(duration.value)}`,
    );
  } catch (error) {
    quote.value = null;
    console.error(error);
  } finally {
    quoting.value = false;
  }
}

async function refresh() {
  loading.value = true;
  try {
    await refreshOverview();
    await refreshQuote();
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
  loading.value = false;
}

watch(duration, () => {
  void refreshQuote();
});

function openCheckout() {
  if (!signedIn.value) {
    toast({
      variant: "destructive",
      title: t("pages.store.sign_in_required"),
    });
    return;
  }
  if (!overview.value?.available) {
    toast({
      variant: "destructive",
      title: t("pages.servers.host.capacity_full"),
    });
    return;
  }
  termsAccepted.value = false;
  checkoutOpen.value = true;
}

async function pay(payWith: "bale" | "ypoint" = "bale") {
  if (paying.value || !quote.value) return;
  if (!termsAccepted.value) {
    toast({ variant: "destructive", title: t("pages.store.terms_required") });
    return;
  }
  paying.value = true;
  try {
    const result = await hostedApi<HostedCheckoutResult>(
      "/hosted-servers/section-host/checkout",
      {
        method: "POST",
        body: {
          sectionMode: mode.value,
          duration: duration.value,
          label: label.value,
          termsAccepted: true,
          payWith,
        },
      },
    );
    checkoutOpen.value = false;
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

async function saveRates() {
  if (rateSaving.value) return;
  rateSaving.value = true;
  try {
    await hostedApi("/hosted-servers/admin/section-host-pricing", {
      method: "POST",
      body: {
        section_host_price_irr_per_hour: tomanToIrr(rateToman.value),
        section_host_price_ypoint_per_hour: Math.max(
          0,
          Math.round(Number(rateYpoint.value) || 0),
        ),
      },
    });
    toast({ title: t("pages.servers.host.admin.rates_saved") });
    await refresh();
  } catch (error) {
    toast({
      variant: "destructive",
      title: hostedErrorMessage(error),
    });
  } finally {
    rateSaving.value = false;
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
          {{ $t("pages.servers.host.description_config") }}
        </p>
      </div>
    </div>

    <PageTransition>
      <div v-if="loading" class="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Skeleton class="h-80 rounded-2xl" />
        <Skeleton class="h-80 rounded-2xl" />
      </div>

      <div v-else class="space-y-8">
        <section
          v-if="isAdmin"
          class="space-y-3 rounded-2xl border border-border/70 bg-card/30 p-4"
        >
          <div class="flex items-center gap-2">
            <Settings2 class="h-4 w-4 text-muted-foreground" />
            <h2 class="m-0 text-base font-semibold">
              {{ $t("pages.servers.host.admin.rates_title") }}
            </h2>
          </div>
          <p class="m-0 text-xs text-muted-foreground">
            {{ $t("pages.servers.host.admin.rates_hint") }}
          </p>
          <div class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.admin.rate_toman") }}</Label>
              <Input v-model.number="rateToman" type="number" min="0" />
            </div>
            <div class="space-y-1.5">
              <Label>{{ $t("pages.servers.host.admin.rate_ypoint") }}</Label>
              <Input v-model.number="rateYpoint" type="number" min="0" />
            </div>
          </div>
          <Button type="button" size="sm" :disabled="rateSaving" @click="saveRates">
            {{ $t("pages.servers.host.admin.save_rates") }}
          </Button>
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

        <div class="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <section
            class="space-y-5 rounded-2xl border border-border/70 bg-card/40 p-5"
          >
            <div>
              <h2 class="m-0 text-lg font-semibold">
                {{ $t("pages.servers.host.configure") }}
              </h2>
              <p class="m-0 mt-1 text-sm text-muted-foreground">
                {{ $t("pages.servers.host.configure_hint") }}
              </p>
            </div>

            <div class="space-y-2">
              <Label>{{ $t("pages.servers.host.pick_mode") }}</Label>
              <div class="grid grid-cols-2 gap-2.5 sm:grid-cols-3">
                <button
                  v-for="m in modeOptions"
                  :key="m.key"
                  type="button"
                  class="group relative aspect-[5/4] overflow-hidden rounded-xl border text-start transition duration-200"
                  :class="
                    mode === m.key
                      ? 'border-[hsl(var(--tac-amber))] ring-2 ring-[hsl(var(--tac-amber)/0.35)]'
                      : 'border-border/70 hover:border-white/25'
                  "
                  @click="mode = m.key"
                >
                  <img
                    :src="m.cover"
                    alt=""
                    class="absolute inset-0 h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
                  />
                  <div
                    class="absolute inset-0 bg-gradient-to-t from-black/90 via-black/45 to-black/20"
                  />
                  <div class="absolute inset-x-0 bottom-0 p-2.5">
                    <div class="text-sm font-semibold text-white">
                      {{ $t(`pages.servers.modes.${m.key}.name`) }}
                    </div>
                    <div class="text-[11px] text-white/55">
                      {{ $t("pages.hosting.slots", { n: m.slots }) }}
                    </div>
                  </div>
                </button>
              </div>
            </div>

            <div class="space-y-2">
              <Label>{{ $t("pages.servers.host.admin.duration") }}</Label>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="d in durationOptions"
                  :key="d"
                  type="button"
                  class="rounded-lg border px-3 py-2 text-sm transition-colors"
                  :class="
                    duration === d
                      ? 'border-primary bg-primary/10 font-semibold'
                      : 'border-border hover:bg-muted/40'
                  "
                  @click="duration = d"
                >
                  {{ formatHostedDuration(d, t) }}
                </button>
              </div>
            </div>

            <div class="space-y-1.5">
              <Label>{{ $t("pages.hosting.server_name") }}</Label>
              <Input
                v-model="label"
                maxlength="64"
                :placeholder="$t('pages.servers.host.name_placeholder')"
              />
            </div>
          </section>

          <aside
            class="relative flex flex-col gap-4 overflow-hidden rounded-2xl border border-border/70 bg-[#0a0c10] p-5 text-white ring-1 ring-white/5"
          >
            <img
              v-if="selectedMode"
              :src="selectedMode.cover"
              alt=""
              class="pointer-events-none absolute inset-0 h-full w-full object-cover opacity-25"
            />
            <div
              class="pointer-events-none absolute inset-0 bg-gradient-to-b from-black/40 via-[#0a0c10]/90 to-[#0a0c10]"
            />
            <div class="relative space-y-1">
              <p class="m-0 text-xs uppercase tracking-[0.16em] text-white/45">
                {{ $t("pages.servers.host.summary") }}
              </p>
              <h3 class="m-0 text-xl font-semibold">
                {{
                  selectedMode
                    ? $t(`pages.servers.modes.${selectedMode.key}.name`)
                    : "—"
                }}
              </h3>
              <p class="m-0 text-sm text-white/55">
                {{ formatHostedDuration(duration, t) }}
                <span v-if="quote"> · {{ quote.hours }}h</span>
              </p>
            </div>

            <div class="relative space-y-2 border-y border-white/10 py-4">
              <div class="flex items-end justify-between gap-3">
                <span class="text-sm text-white/55">
                  {{ $t("pages.servers.host.price") }}
                </span>
                <span
                  v-if="quoting"
                  class="text-sm text-white/40"
                >
                  …
                </span>
                <span
                  v-else-if="quote"
                  class="font-mono text-2xl font-bold tabular-nums"
                >
                  {{ formatPrice(quote.price_irr) }}
                </span>
              </div>
              <div
                v-if="quote?.price_ypoint"
                class="flex items-center justify-end gap-1.5 font-mono text-sm text-[hsl(var(--tac-amber))]"
              >
                <img
                  src="/img/ypoint-logo.png"
                  alt=""
                  class="h-4 w-4 object-contain"
                />
                {{ quote.price_ypoint }}
              </div>
              <p class="m-0 text-xs text-white/40">
                {{
                  $t("pages.servers.host.rate_hint", {
                    toman: formatTomanAmount(
                      overview?.price_irr_per_hour || 0,
                      locale.startsWith("fa") ? "fa-IR" : "en-US",
                    ),
                    ypoint: overview?.price_ypoint_per_hour || 0,
                  })
                }}
              </p>
            </div>

            <Button
              type="button"
              size="lg"
              class="relative mt-auto w-full"
              :disabled="!overview?.available || !quote || quoting"
              @click="openCheckout"
            >
              <Clock class="h-4 w-4" />
              {{ $t("pages.servers.host.rent") }}
            </Button>
          </aside>
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
              class="flex items-center gap-3 rounded-xl border border-border bg-card/40 p-4 transition-colors hover:bg-muted/40"
            >
              <Server class="h-8 w-8 shrink-0 text-muted-foreground" />
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <span class="truncate font-semibold">{{ server.label }}</span>
                  <Badge :variant="hostedStatusVariant(server.status)">
                    {{ $t(`pages.hosting.status.${server.status}`) }}
                  </Badge>
                  <Badge v-if="server.section_mode" variant="outline">
                    {{ $t(`pages.servers.modes.${server.section_mode}.name`) }}
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

    <Dialog v-model:open="checkoutOpen">
      <DialogScrollContent class="max-w-md">
        <DialogHeader>
          <DialogTitle>{{ $t("pages.servers.host.rent") }}</DialogTitle>
          <DialogDescription>
            <span v-if="selectedMode">
              {{ $t(`pages.servers.modes.${selectedMode.key}.name`) }}
            </span>
            · {{ formatHostedDuration(duration, t) }}
            <span v-if="quote"> · {{ formatPrice(quote.price_irr) }}</span>
          </DialogDescription>
        </DialogHeader>

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
          class="mt-4 flex w-full items-start gap-3 rounded-lg border px-3 py-3 text-start"
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

        <DialogFooter class="gap-2">
          <Button
            type="button"
            variant="ghost"
            :disabled="paying"
            @click="checkoutOpen = false"
          >
            {{ $t("common.cancel") }}
          </Button>
          <Button
            v-if="quote?.price_ypoint"
            type="button"
            variant="outline"
            :disabled="paying || !termsAccepted || !canAffordYpoint"
            @click="pay('ypoint')"
          >
            <img
              src="/img/ypoint-logo.png"
              alt=""
              class="h-4 w-4 object-contain"
            />
            {{
              $t("pages.store.pay_with_ypoint", { n: quote.price_ypoint })
            }}
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
      </DialogScrollContent>
    </Dialog>
  </div>
</template>
