<script setup lang="ts">
import gql from "graphql-tag";
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import { Wallet, Search, Plus, Minus } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Badge } from "~/components/ui/badge";
import { Textarea } from "~/components/ui/textarea";
import { toast } from "~/components/ui/toast";
import { formatTomanAmount } from "~/utilities/irrToman";

definePageMeta({
  middleware: "admin",
});

const { t, locale } = useI18n();
const { client: apollo } = useApolloClient();
const apiDomain = useRuntimeConfig().public.apiDomain as string;

type Tab = "payments" | "subscriptions" | "adjust";
const tab = ref<Tab>("payments");

type OrderRow = {
  id: string;
  amount_irr: number;
  status: string;
  created_at: string;
  paid_at: string | null;
  buyer_steam_id: string;
  buyer?: { name: string | null; avatar_url: string | null } | null;
  product?: { title: string; ypoint_amount: number | null } | null;
};

type VipRow = {
  id: string;
  steam_id: string;
  server_id: string;
  expires_at: string | null;
  granted_at: string;
  player?: { name: string | null; avatar_url: string | null } | null;
  server?: { label: string | null; host: string; port: number } | null;
};

type PlayerHit = {
  steam_id: string;
  name: string | null;
  avatar_url: string | null;
  ypoint_balance: number;
};

const orders = ref<OrderRow[]>([]);
const vipGrants = ref<VipRow[]>([]);
const loadingOrders = ref(false);
const loadingVip = ref(false);
const statusFilter = ref<string>("all");

const playerQuery = ref("");
const playerHits = ref<PlayerHit[]>([]);
const searchingPlayers = ref(false);
const selectedPlayer = ref<PlayerHit | null>(null);
const adjustAmount = ref<number>(10);
const adjustNote = ref("");
const adjusting = ref(false);

const ORDERS_QUERY = gql`
  query AdminFinanceOrders($where: store_orders_bool_exp!, $limit: Int!) {
    store_orders(
      where: $where
      order_by: { created_at: desc }
      limit: $limit
    ) {
      id
      amount_irr
      status
      created_at
      paid_at
      buyer_steam_id
      buyer {
        name
        avatar_url
      }
      product {
        title
        ypoint_amount
      }
    }
  }
`;

const VIP_QUERY = gql`
  query AdminFinanceVip($limit: Int!) {
    store_vip_grants(order_by: { granted_at: desc }, limit: $limit) {
      id
      steam_id
      server_id
      expires_at
      granted_at
      player {
        name
        avatar_url
      }
      server {
        label
        host
        port
      }
    }
  }
`;

async function loadOrders() {
  loadingOrders.value = true;
  try {
    const where =
      statusFilter.value === "all"
        ? {}
        : { status: { _eq: statusFilter.value } };
    const { data } = await apollo.query<{ store_orders: OrderRow[] }>({
      query: ORDERS_QUERY,
      variables: { where, limit: 100 },
      fetchPolicy: "network-only",
    });
    orders.value = data?.store_orders ?? [];
  } catch (error) {
    console.error(error);
    toast({
      title: t("pages.settings.application.finance.load_failed"),
      variant: "destructive",
    });
  } finally {
    loadingOrders.value = false;
  }
}

async function loadVip() {
  loadingVip.value = true;
  try {
    const { data } = await apollo.query<{ store_vip_grants: VipRow[] }>({
      query: VIP_QUERY,
      variables: { limit: 100 },
      fetchPolicy: "network-only",
    });
    vipGrants.value = data?.store_vip_grants ?? [];
  } catch (error) {
    console.error(error);
    toast({
      title: t("pages.settings.application.finance.load_failed"),
      variant: "destructive",
    });
  } finally {
    loadingVip.value = false;
  }
}

async function searchPlayers() {
  const q = playerQuery.value.trim();
  if (!q) {
    playerHits.value = [];
    return;
  }
  searchingPlayers.value = true;
  try {
    const data = await $fetch<{ players: PlayerHit[] }>(
      `https://${apiDomain}/ypoint/admin/players`,
      {
        credentials: "include",
        query: { q },
      },
    );
    playerHits.value = data.players ?? [];
  } catch (error) {
    console.error(error);
    toast({
      title: t("pages.settings.application.finance.search_failed"),
      variant: "destructive",
    });
  } finally {
    searchingPlayers.value = false;
  }
}

function selectPlayer(p: PlayerHit) {
  selectedPlayer.value = p;
  playerHits.value = [];
  playerQuery.value = p.name || p.steam_id;
}

async function applyAdjust(sign: 1 | -1) {
  if (!selectedPlayer.value) {
    toast({
      title: t("pages.settings.application.finance.pick_player"),
      variant: "destructive",
    });
    return;
  }
  const amount = Math.abs(Math.trunc(Number(adjustAmount.value) || 0));
  if (amount <= 0) {
    toast({
      title: t("pages.settings.application.finance.invalid_amount"),
      variant: "destructive",
    });
    return;
  }
  adjusting.value = true;
  try {
    const data = await $fetch<{
      success: boolean;
      balance: number;
      delta: number;
    }>(`https://${apiDomain}/ypoint/admin/adjust`, {
      method: "POST",
      credentials: "include",
      body: {
        steamId: selectedPlayer.value.steam_id,
        delta: sign * amount,
        note: adjustNote.value.trim() || undefined,
      },
    });
    selectedPlayer.value = {
      ...selectedPlayer.value,
      ypoint_balance: data.balance,
    };
    toast({
      title: t("pages.settings.application.finance.adjust_ok"),
      description: t("pages.settings.application.finance.adjust_ok_desc", {
        balance: data.balance,
        delta: data.delta > 0 ? `+${data.delta}` : String(data.delta),
      }),
    });
    adjustNote.value = "";
  } catch (error: any) {
    const msg =
      error?.data?.message ||
      error?.statusMessage ||
      t("pages.settings.application.finance.adjust_failed");
    toast({ title: String(msg), variant: "destructive" });
  } finally {
    adjusting.value = false;
  }
}

function formatWhen(iso: string | null | undefined) {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString(locale.value || undefined);
  } catch {
    return iso;
  }
}

function statusVariant(status: string) {
  if (status === "paid") return "default" as const;
  if (status === "pending") return "secondary" as const;
  return "outline" as const;
}

const vipActive = computed(() =>
  vipGrants.value.filter(
    (g) => !g.expires_at || new Date(g.expires_at).getTime() > Date.now(),
  ),
);

watch(statusFilter, () => {
  void loadOrders();
});

watch(tab, (next) => {
  if (next === "payments" && !orders.value.length) void loadOrders();
  if (next === "subscriptions" && !vipGrants.value.length) void loadVip();
});

onMounted(() => {
  void loadOrders();
});
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <div class="space-y-6">
        <SettingsSection
          id="finance-overview"
          :title="$t('pages.settings.application.finance.title')"
          :description="$t('pages.settings.application.finance.description')"
        >
          <div class="flex flex-wrap gap-2">
            <Button
              size="sm"
              :variant="tab === 'payments' ? 'default' : 'outline'"
              @click="tab = 'payments'"
            >
              {{ $t("pages.settings.application.finance.tabs.payments") }}
            </Button>
            <Button
              size="sm"
              :variant="tab === 'subscriptions' ? 'default' : 'outline'"
              @click="tab = 'subscriptions'"
            >
              {{ $t("pages.settings.application.finance.tabs.subscriptions") }}
            </Button>
            <Button
              size="sm"
              :variant="tab === 'adjust' ? 'default' : 'outline'"
              class="gap-1.5"
              @click="tab = 'adjust'"
            >
              <Wallet class="h-3.5 w-3.5" />
              {{ $t("pages.settings.application.finance.tabs.adjust") }}
            </Button>
          </div>
        </SettingsSection>

        <!-- Payments -->
        <SettingsSection
          v-if="tab === 'payments'"
          id="finance-payments"
          :title="$t('pages.settings.application.finance.payments_title')"
          :description="
            $t('pages.settings.application.finance.payments_description')
          "
        >
          <div class="mb-3 flex flex-wrap items-center gap-2">
            <Label class="text-xs text-muted-foreground">{{
              $t("pages.settings.application.finance.status")
            }}</Label>
            <select
              v-model="statusFilter"
              class="h-8 rounded-md border border-border bg-background px-2 text-sm"
            >
              <option value="all">
                {{ $t("pages.settings.application.finance.status_all") }}
              </option>
              <option value="paid">paid</option>
              <option value="pending">pending</option>
              <option value="cancelled">cancelled</option>
              <option value="failed">failed</option>
            </select>
            <Button
              size="sm"
              variant="outline"
              :disabled="loadingOrders"
              @click="loadOrders"
            >
              {{ $t("pages.settings.application.finance.refresh") }}
            </Button>
          </div>

          <p
            v-if="loadingOrders"
            class="text-sm text-muted-foreground"
          >
            {{ $t("pages.settings.application.finance.loading") }}
          </p>
          <p
            v-else-if="!orders.length"
            class="text-sm text-muted-foreground"
          >
            {{ $t("pages.settings.application.finance.payments_empty") }}
          </p>
          <div v-else class="overflow-x-auto rounded-md border border-border">
            <table class="w-full min-w-[40rem] text-left text-sm">
              <thead class="border-b border-border bg-muted/40 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_player") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_product") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_amount") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_status") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_when") }}
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="row in orders"
                  :key="row.id"
                  class="border-b border-border/60 last:border-0"
                >
                  <td class="px-3 py-2">
                    <div class="font-medium">
                      {{ row.buyer?.name || row.buyer_steam_id }}
                    </div>
                    <div class="font-mono text-[0.65rem] text-muted-foreground">
                      {{ row.buyer_steam_id }}
                    </div>
                  </td>
                  <td class="px-3 py-2">
                    {{ row.product?.title || "—" }}
                    <Badge
                      v-if="row.product?.ypoint_amount"
                      variant="secondary"
                      class="ml-1 align-middle"
                    >
                      +{{ row.product.ypoint_amount }} YP
                    </Badge>
                  </td>
                  <td class="px-3 py-2 whitespace-nowrap">
                    {{ formatTomanAmount(row.amount_irr, locale) }}
                    {{ $t("pages.settings.application.finance.toman") }}
                  </td>
                  <td class="px-3 py-2">
                    <Badge :variant="statusVariant(row.status)">{{
                      row.status
                    }}</Badge>
                  </td>
                  <td class="px-3 py-2 text-muted-foreground whitespace-nowrap">
                    {{ formatWhen(row.paid_at || row.created_at) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </SettingsSection>

        <!-- Subscriptions / VIP -->
        <SettingsSection
          v-if="tab === 'subscriptions'"
          id="finance-vip"
          :title="$t('pages.settings.application.finance.vip_title')"
          :description="$t('pages.settings.application.finance.vip_description')"
        >
          <div class="mb-3 flex items-center gap-2">
            <Badge variant="secondary">
              {{
                $t("pages.settings.application.finance.vip_active_count", {
                  count: vipActive.length,
                })
              }}
            </Badge>
            <Button
              size="sm"
              variant="outline"
              :disabled="loadingVip"
              @click="loadVip"
            >
              {{ $t("pages.settings.application.finance.refresh") }}
            </Button>
          </div>

          <p v-if="loadingVip" class="text-sm text-muted-foreground">
            {{ $t("pages.settings.application.finance.loading") }}
          </p>
          <p
            v-else-if="!vipGrants.length"
            class="text-sm text-muted-foreground"
          >
            {{ $t("pages.settings.application.finance.vip_empty") }}
          </p>
          <div v-else class="overflow-x-auto rounded-md border border-border">
            <table class="w-full min-w-[36rem] text-left text-sm">
              <thead class="border-b border-border bg-muted/40 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_player") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_server") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_expires") }}
                  </th>
                  <th class="px-3 py-2 font-medium">
                    {{ $t("pages.settings.application.finance.col_granted") }}
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="row in vipGrants"
                  :key="row.id"
                  class="border-b border-border/60 last:border-0"
                >
                  <td class="px-3 py-2">
                    <div class="font-medium">
                      {{ row.player?.name || row.steam_id }}
                    </div>
                    <div class="font-mono text-[0.65rem] text-muted-foreground">
                      {{ row.steam_id }}
                    </div>
                  </td>
                  <td class="px-3 py-2">
                    {{
                      row.server?.label ||
                      (row.server
                        ? `${row.server.host}:${row.server.port}`
                        : row.server_id)
                    }}
                  </td>
                  <td class="px-3 py-2 whitespace-nowrap">
                    <Badge
                      v-if="!row.expires_at"
                      variant="secondary"
                    >
                      {{ $t("pages.settings.application.finance.vip_permanent") }}
                    </Badge>
                    <span v-else>{{ formatWhen(row.expires_at) }}</span>
                  </td>
                  <td class="px-3 py-2 text-muted-foreground whitespace-nowrap">
                    {{ formatWhen(row.granted_at) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </SettingsSection>

        <!-- Adjust Ypoints -->
        <SettingsSection
          v-if="tab === 'adjust'"
          id="finance-adjust"
          :title="$t('pages.settings.application.finance.adjust_title')"
          :description="
            $t('pages.settings.application.finance.adjust_description')
          "
        >
          <div class="max-w-lg space-y-4">
            <div class="space-y-2">
              <Label>{{
                $t("pages.settings.application.finance.search_player")
              }}</Label>
              <div class="flex gap-2">
                <Input
                  v-model="playerQuery"
                  :placeholder="
                    $t(
                      'pages.settings.application.finance.search_placeholder',
                    )
                  "
                  @keydown.enter.prevent="searchPlayers"
                />
                <Button
                  type="button"
                  variant="outline"
                  :disabled="searchingPlayers"
                  @click="searchPlayers"
                >
                  <Search class="h-4 w-4" />
                </Button>
              </div>
              <ul
                v-if="playerHits.length"
                class="rounded-md border border-border divide-y divide-border"
              >
                <li
                  v-for="p in playerHits"
                  :key="p.steam_id"
                  class="flex cursor-pointer items-center justify-between gap-2 px-3 py-2 hover:bg-muted/50"
                  @click="selectPlayer(p)"
                >
                  <div>
                    <div class="font-medium">{{ p.name || p.steam_id }}</div>
                    <div class="font-mono text-[0.65rem] text-muted-foreground">
                      {{ p.steam_id }}
                    </div>
                  </div>
                  <Badge variant="secondary">{{ p.ypoint_balance }} YP</Badge>
                </li>
              </ul>
            </div>

            <div
              v-if="selectedPlayer"
              class="rounded-md border border-border bg-muted/20 px-3 py-2"
            >
              <div class="text-sm font-medium">
                {{ selectedPlayer.name || selectedPlayer.steam_id }}
              </div>
              <div class="font-mono text-[0.65rem] text-muted-foreground">
                {{ selectedPlayer.steam_id }}
              </div>
              <div class="mt-1 text-sm">
                {{ $t("pages.settings.application.finance.current_balance") }}:
                <strong>{{ selectedPlayer.ypoint_balance }}</strong> YP
              </div>
            </div>

            <div class="space-y-2">
              <Label>{{
                $t("pages.settings.application.finance.amount")
              }}</Label>
              <Input
                v-model.number="adjustAmount"
                type="number"
                min="1"
                step="1"
              />
            </div>

            <div class="space-y-2">
              <Label>{{ $t("pages.settings.application.finance.note") }}</Label>
              <Textarea
                v-model="adjustNote"
                rows="2"
                :placeholder="
                  $t('pages.settings.application.finance.note_placeholder')
                "
              />
            </div>

            <div class="flex flex-wrap gap-2">
              <Button
                class="gap-1.5"
                :disabled="adjusting || !selectedPlayer"
                @click="applyAdjust(1)"
              >
                <Plus class="h-4 w-4" />
                {{ $t("pages.settings.application.finance.add") }}
              </Button>
              <Button
                variant="destructive"
                class="gap-1.5"
                :disabled="adjusting || !selectedPlayer"
                @click="applyAdjust(-1)"
              >
                <Minus class="h-4 w-4" />
                {{ $t("pages.settings.application.finance.remove") }}
              </Button>
            </div>
          </div>
        </SettingsSection>
      </div>
    </PageTransition>
  </SettingsPage>
</template>
