<script setup lang="ts">
import gql from "graphql-tag";
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useApolloClient } from "@vue/apollo-composable";
import { Plus, Pencil, Trash2, Store as StoreIcon } from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import SettingsPage from "~/components/settings/SettingsPage.vue";
import SettingsSection from "~/components/settings/SettingsSection.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Switch } from "~/components/ui/switch";
import { Textarea } from "~/components/ui/textarea";
import { Badge } from "~/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "~/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "~/components/ui/alert-dialog";
import { toast } from "~/components/ui/toast";

definePageMeta({
  middleware: "admin",
});

const { t } = useI18n();
const { client: apollo } = useApolloClient();

type Product = {
  id: string;
  title: string;
  slug: string;
  description: string;
  price_irr: number;
  image_url: string | null;
  active: boolean;
  sort_order: number;
  ypoint_amount: number | null;
};

const products = ref<Product[]>([]);
const loading = ref(true);
const dialogOpen = ref(false);
const submitting = ref(false);
const baleStatus = ref<{
  configured: boolean;
  botUsername: string | null;
  webhookHint: string;
} | null>(null);

const emptyForm = () => ({
  id: "" as string,
  title: "",
  slug: "",
  description: "",
  price_irr: 0,
  ypoint_amount: null as number | null,
  image_url: "",
  active: true,
  sort_order: 0,
});

const form = reactive(emptyForm());

const LIST_QUERY = gql`
  query AdminStoreProducts {
    store_products(order_by: [{ sort_order: asc }, { created_at: desc }]) {
      id
      title
      slug
      description
      price_irr
      ypoint_amount
      image_url
      active
      sort_order
    }
  }
`;

const INSERT = gql`
  mutation InsertStoreProduct($object: store_products_insert_input!) {
    insert_store_products_one(object: $object) {
      id
    }
  }
`;

const UPDATE = gql`
  mutation UpdateStoreProduct(
    $id: uuid!
    $set: store_products_set_input!
  ) {
    update_store_products_by_pk(pk_columns: { id: $id }, _set: $set) {
      id
    }
  }
`;

const DELETE = gql`
  mutation DeleteStoreProduct($id: uuid!) {
    delete_store_products_by_pk(id: $id) {
      id
    }
  }
`;

function slugify(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "")
    .slice(0, 64);
}

function openCreate() {
  Object.assign(form, emptyForm());
  dialogOpen.value = true;
}

function openEdit(product: Product) {
  Object.assign(form, {
    id: product.id,
    title: product.title,
    slug: product.slug,
    description: product.description || "",
    price_irr: product.price_irr,
    ypoint_amount: product.ypoint_amount,
    image_url: product.image_url || "",
    active: product.active,
    sort_order: product.sort_order,
  });
  dialogOpen.value = true;
}

async function refresh() {
  loading.value = true;
  try {
    const { data } = await apollo.query({
      query: LIST_QUERY,
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

async function refreshBaleStatus() {
  try {
    baleStatus.value = await $fetch("/api/store/status");
  } catch {
    baleStatus.value = null;
  }
}

async function save() {
  if (submitting.value) return;
  submitting.value = true;
  try {
    const object = {
      title: form.title.trim(),
      slug: (form.slug || slugify(form.title)).trim(),
      description: form.description.trim(),
      price_irr: Math.max(0, Math.round(Number(form.price_irr) || 0)),
      ypoint_amount:
        form.ypoint_amount === null || form.ypoint_amount === ("" as any)
          ? null
          : Math.max(0, Math.round(Number(form.ypoint_amount) || 0)) || null,
      image_url: form.image_url.trim() || null,
      active: form.active,
      sort_order: Math.round(Number(form.sort_order) || 0),
      updated_at: new Date().toISOString(),
    };
    if (!object.title || !object.slug) {
      throw new Error(t("pages.settings.application.store.errors.required"));
    }

    if (form.id) {
      await apollo.mutate({
        mutation: UPDATE,
        variables: { id: form.id, set: object },
      });
    } else {
      const { updated_at: _u, ...insertObject } = object;
      await apollo.mutate({
        mutation: INSERT,
        variables: { object: insertObject },
      });
    }
    dialogOpen.value = false;
    toast({ title: t("pages.settings.application.update_success") });
    await refresh();
  } catch (error: any) {
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: error?.message || String(error),
    });
  } finally {
    submitting.value = false;
  }
}

async function remove(product: Product) {
  try {
    await apollo.mutate({
      mutation: DELETE,
      variables: { id: product.id },
    });
    toast({ title: t("pages.settings.application.store.deleted") });
    await refresh();
  } catch (error: any) {
    const message =
      error?.graphQLErrors?.[0]?.message ||
      error?.message ||
      String(error);
    // Orders keep a FK to the product — archive instead of hard-delete.
    if (/foreign key|store_orders_product_id/i.test(message)) {
      try {
        await apollo.mutate({
          mutation: UPDATE,
          variables: {
            id: product.id,
            set: {
              active: false,
              updated_at: new Date().toISOString(),
            },
          },
        });
        toast({
          title: t("pages.settings.application.store.archived_title"),
          description: t("pages.settings.application.store.archived_body"),
        });
        await refresh();
        return;
      } catch (archiveError: any) {
        toast({
          variant: "destructive",
          title: t("common.error"),
          description: archiveError?.message || String(archiveError),
        });
        return;
      }
    }
    toast({
      variant: "destructive",
      title: t("common.error"),
      description: message,
    });
  }
}

function formatPrice(irr: number) {
  return `${irr.toLocaleString("en-US")} IRR`;
}

const baleHint = computed(() => {
  if (!baleStatus.value) {
    return t("pages.settings.application.store.bale.unknown");
  }
  if (baleStatus.value.configured) {
    return t("pages.settings.application.store.bale.ready", {
      bot: baleStatus.value.botUsername || "—",
    });
  }
  return t("pages.settings.application.store.bale.missing");
});

onMounted(() => {
  void refresh();
  void refreshBaleStatus();
});
</script>

<template>
  <SettingsPage>
    <PageTransition :delay="0">
      <div class="space-y-6">
        <SettingsSection
          id="store-bale"
          :title="$t('pages.settings.application.store.bale.title')"
          :description="$t('pages.settings.application.store.bale.description')"
        >
          <p class="text-sm text-muted-foreground">{{ baleHint }}</p>
          <p
            v-if="baleStatus?.webhookHint"
            class="mt-2 font-mono text-xs text-muted-foreground"
          >
            {{
              $t("pages.settings.application.store.bale.webhook", {
                url: baleStatus.webhookHint,
              })
            }}
          </p>
        </SettingsSection>

        <SettingsSection
          id="store-products"
          :title="$t('pages.settings.application.store.section')"
          :description="$t('pages.settings.application.store.description')"
        >
          <div class="mb-4 flex justify-end">
            <Button type="button" size="sm" @click="openCreate">
              <Plus class="me-1 h-4 w-4" />
              {{ $t("pages.settings.application.store.add") }}
            </Button>
          </div>

          <div v-if="loading" class="text-sm text-muted-foreground">
            {{ $t("common.loading") }}
          </div>

          <p
            v-else-if="products.length === 0"
            class="text-sm text-muted-foreground"
          >
            {{ $t("pages.settings.application.store.empty") }}
          </p>

          <ul v-else class="divide-y divide-border rounded-md border">
            <li
              v-for="product in products"
              :key="product.id"
              class="flex items-center gap-3 px-3 py-3"
            >
              <div
                class="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded bg-muted"
              >
                <img
                  v-if="product.image_url"
                  :src="product.image_url"
                  alt=""
                  class="h-full w-full object-cover"
                />
                <StoreIcon v-else class="h-4 w-4 text-muted-foreground" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="truncate font-medium">{{ product.title }}</span>
                  <Badge variant="outline">{{ formatPrice(product.price_irr) }}</Badge>
                  <Badge v-if="product.ypoint_amount" variant="secondary">
                    +{{ product.ypoint_amount }} YP
                  </Badge>
                  <Badge :variant="product.active ? 'default' : 'secondary'">
                    {{
                      product.active
                        ? $t("pages.settings.application.store.active")
                        : $t("pages.settings.application.store.inactive")
                    }}
                  </Badge>
                </div>
                <p class="truncate text-xs text-muted-foreground">
                  /{{ product.slug }}
                </p>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                @click="openEdit(product)"
              >
                <Pencil class="h-4 w-4" />
              </Button>
              <AlertDialog>
                <AlertDialogTrigger as-child>
                  <Button type="button" variant="ghost" size="icon">
                    <Trash2 class="h-4 w-4 text-destructive" />
                  </Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>
                      {{ $t("pages.settings.application.store.delete_title") }}
                    </AlertDialogTitle>
                    <AlertDialogDescription>
                      {{
                        $t("pages.settings.application.store.delete_body", {
                          title: product.title,
                        })
                      }}
                    </AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>{{ $t("common.cancel") }}</AlertDialogCancel>
                    <AlertDialogAction @click="remove(product)">
                      {{ $t("common.delete") }}
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
            </li>
          </ul>
        </SettingsSection>
      </div>
    </PageTransition>

    <Dialog v-model:open="dialogOpen">
      <DialogContent class="max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {{
              form.id
                ? $t("pages.settings.application.store.edit")
                : $t("pages.settings.application.store.add")
            }}
          </DialogTitle>
        </DialogHeader>

        <form class="space-y-4" @submit.prevent="save">
          <div class="space-y-2">
            <Label>{{ $t("pages.settings.application.store.fields.title") }}</Label>
            <Input
              v-model="form.title"
              required
              @blur="form.slug = form.slug || slugify(form.title)"
            />
          </div>
          <div class="space-y-2">
            <Label>{{ $t("pages.settings.application.store.fields.slug") }}</Label>
            <Input v-model="form.slug" required />
          </div>
          <div class="space-y-2">
            <Label>{{
              $t("pages.settings.application.store.fields.description")
            }}</Label>
            <Textarea v-model="form.description" rows="3" />
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-2">
              <Label>{{
                $t("pages.settings.application.store.fields.price_irr")
              }}</Label>
              <Input v-model.number="form.price_irr" type="number" min="0" required />
            </div>
            <div class="space-y-2">
              <Label>{{
                $t("pages.settings.application.store.fields.ypoint_amount")
              }}</Label>
              <Input
                v-model.number="form.ypoint_amount"
                type="number"
                min="0"
                placeholder="0"
              />
            </div>
          </div>
          <div class="space-y-2">
            <Label>{{
              $t("pages.settings.application.store.fields.sort_order")
            }}</Label>
            <Input v-model.number="form.sort_order" type="number" />
          </div>
          <div class="space-y-2">
            <Label>{{
              $t("pages.settings.application.store.fields.image_url")
            }}</Label>
            <Input v-model="form.image_url" type="url" placeholder="https://" />
          </div>
          <div class="flex items-center justify-between rounded-md border px-3 py-2">
            <Label>{{ $t("pages.settings.application.store.fields.active") }}</Label>
            <Switch v-model="form.active" />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" @click="dialogOpen = false">
              {{ $t("common.cancel") }}
            </Button>
            <Button type="submit" :disabled="submitting">
              {{ $t("common.save") }}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  </SettingsPage>
</template>
