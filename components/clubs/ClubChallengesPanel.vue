<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";
import { clubErrorMessage } from "~/utilities/clubErrors";

const DIFFICULTIES = [
  "easy",
  "medium",
  "hard",
  "elite",
  "legendary",
] as const;
const CATEGORIES = [
  "aim",
  "clutch",
  "utility",
  "objective",
  "eco",
  "grind",
  "teamwork",
  "consistency",
  "social",
  "fun",
] as const;

type CatalogRow = {
  id: string;
  code: string;
  difficulty: string;
  category: string;
  title: string;
  title_fa: string;
  description: string;
  description_fa: string;
};
type Activation = {
  id: string;
  catalog_id: string;
  ends_at: string;
  prize_ypoints: number | null;
  prize_other: string;
  active: boolean;
  created_at: string;
  catalog?: CatalogRow | null;
};

const props = defineProps<{
  clubId: string;
  isOwner: boolean;
}>();

const { t, locale } = useI18n();
const { client } = useApolloClient();

const catalog = ref<CatalogRow[]>([]);
const activations = ref<Activation[]>([]);
const loading = ref(true);
const busy = ref(false);
const error = ref("");
const search = ref("");
const difficultyFilter = ref("");
const categoryFilter = ref("");
const selected = ref<string[]>([]);
const prizeYpoints = ref<number | "">("");
const prizeOther = ref("");
const endsAt = ref(defaultEndLocal());

function defaultEndLocal() {
  const d = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const fa = computed(() => String(locale.value).startsWith("fa"));

function catalogTitle(row: CatalogRow) {
  return fa.value ? row.title_fa || row.title : row.title;
}
function catalogDesc(row: CatalogRow) {
  return fa.value ? row.description_fa || row.description : row.description;
}

const liveIds = computed(() => {
  const now = Date.now();
  return new Set(
    activations.value
      .filter((row) => row.active && new Date(row.ends_at).getTime() > now)
      .map((row) => row.catalog_id),
  );
});

const live = computed(() => {
  const now = Date.now();
  return activations.value.filter(
    (row) => row.active && new Date(row.ends_at).getTime() > now,
  );
});

const past = computed(() => {
  const now = Date.now();
  return activations.value.filter(
    (row) => !row.active || new Date(row.ends_at).getTime() <= now,
  );
});

const filteredCatalog = computed(() => {
  const q = search.value.trim().toLowerCase();
  return catalog.value.filter((row) => {
    if (difficultyFilter.value && row.difficulty !== difficultyFilter.value) {
      return false;
    }
    if (categoryFilter.value && row.category !== categoryFilter.value) {
      return false;
    }
    if (!q) return true;
    return (
      catalogTitle(row).toLowerCase().includes(q) ||
      catalogDesc(row).toLowerCase().includes(q) ||
      row.code.toLowerCase().includes(q)
    );
  });
});

function toggle(id: string) {
  if (liveIds.value.has(id)) return;
  if (selected.value.includes(id)) {
    selected.value = selected.value.filter((row) => row !== id);
  } else {
    selected.value = [...selected.value, id];
  }
}

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const { data } = await client.query({
      query: gql`
        query ClubChallenges($clubId: uuid!) {
          club_challenge_catalog(order_by: { sort_order: asc }) {
            id
            code
            difficulty
            category
            title
            title_fa
            description
            description_fa
          }
          club_challenges(
            where: { club_id: { _eq: $clubId } }
            order_by: [{ created_at: desc }]
          ) {
            id
            catalog_id
            ends_at
            prize_ypoints
            prize_other
            active
            created_at
            catalog {
              id
              code
              difficulty
              category
              title
              title_fa
              description
              description_fa
            }
          }
        }
      `,
      variables: { clubId: props.clubId },
      fetchPolicy: "network-only",
      context: { optional: true },
    });
    catalog.value = data?.club_challenge_catalog ?? [];
    activations.value = data?.club_challenges ?? [];
  } catch (e) {
    error.value = clubErrorMessage(e, t);
    catalog.value = [];
    activations.value = [];
  } finally {
    loading.value = false;
  }
}

async function activate() {
  error.value = "";
  const ids = selected.value.filter((id) => !liveIds.value.has(id));
  if (!ids.length) {
    error.value = t("pages.clubs.challenges.pick_one");
    return;
  }
  const ends = new Date(endsAt.value);
  if (Number.isNaN(ends.getTime()) || ends.getTime() <= Date.now()) {
    error.value = t("pages.clubs.challenges.need_future_end");
    return;
  }
  const ypoints =
    prizeYpoints.value === "" || prizeYpoints.value == null
      ? null
      : Number(prizeYpoints.value);
  const other = prizeOther.value.trim();
  if (!(ypoints && ypoints > 0) && !other) {
    error.value = t("pages.clubs.challenges.need_prize");
    return;
  }
  busy.value = true;
  try {
    await client.mutate({
      mutation: gql`
        mutation ActivateClubChallenges($objects: [club_challenges_insert_input!]!) {
          insert_club_challenges(objects: $objects) {
            affected_rows
          }
        }
      `,
      variables: {
        objects: ids.map((catalog_id) => ({
          club_id: props.clubId,
          catalog_id,
          ends_at: ends.toISOString(),
          prize_ypoints: ypoints && ypoints > 0 ? ypoints : null,
          prize_other: other,
        })),
      },
    });
    selected.value = [];
    await load();
  } catch (e) {
    error.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

async function deactivate(id: string) {
  error.value = "";
  busy.value = true;
  try {
    await client.mutate({
      mutation: gql`
        mutation DeactivateClubChallenge($id: uuid!) {
          update_club_challenges_by_pk(
            pk_columns: { id: $id }
            _set: { active: false }
          ) {
            id
          }
        }
      `,
      variables: { id },
    });
    await load();
  } catch (e) {
    error.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

function prizeLabel(row: Activation) {
  const parts: string[] = [];
  if (row.prize_ypoints && row.prize_ypoints > 0) {
    parts.push(
      t("pages.clubs.challenges.ypoints_amount", {
        amount: String(row.prize_ypoints),
      }),
    );
  }
  if (row.prize_other?.trim()) parts.push(row.prize_other.trim());
  return parts.join(" · ");
}

function diffClass(diff: string) {
  switch (diff) {
    case "easy":
      return "border-emerald-500/40 bg-emerald-500/10 text-emerald-300";
    case "medium":
      return "border-sky-500/40 bg-sky-500/10 text-sky-300";
    case "hard":
      return "border-orange-500/40 bg-orange-500/10 text-orange-300";
    case "elite":
      return "border-violet-500/40 bg-violet-500/10 text-violet-300";
    default:
      return "border-[hsl(var(--tac-amber))]/40 bg-[hsl(var(--tac-amber))]/10 text-[hsl(var(--tac-amber))]";
  }
}

watch(
  () => props.clubId,
  () => {
    void load();
  },
);

onMounted(() => {
  void load();
});
</script>

<template>
  <div class="space-y-5">
    <p class="text-sm text-muted-foreground">
      {{ t("pages.clubs.challenges.hint") }}
    </p>
    <p v-if="error" class="text-sm text-destructive">{{ error }}</p>
    <p v-if="loading" class="text-sm text-muted-foreground">
      {{ t("pages.clubs.challenges.loading") }}
    </p>

    <div>
      <h3 class="mb-2 text-sm font-semibold">
        {{ t("pages.clubs.challenges.active") }}
        <span class="font-normal text-muted-foreground">({{ live.length }})</span>
      </h3>
      <div v-if="live.length" class="grid gap-3 sm:grid-cols-2">
        <article
          v-for="row in live"
          :key="row.id"
          class="rounded-xl border border-border/70 bg-card/40 p-4"
        >
          <div class="flex flex-wrap items-start justify-between gap-2">
            <div>
              <h4 class="font-semibold">
                {{ row.catalog ? catalogTitle(row.catalog) : row.catalog_id }}
              </h4>
              <p class="mt-1 text-xs text-muted-foreground">
                {{ row.catalog ? catalogDesc(row.catalog) : "" }}
              </p>
            </div>
            <span
              v-if="row.catalog"
              class="rounded-full border px-2 py-0.5 text-[11px] font-medium uppercase"
              :class="diffClass(row.catalog.difficulty)"
            >
              {{ t(`pages.clubs.challenges.difficulty.${row.catalog.difficulty}`) }}
            </span>
          </div>
          <p class="mt-3 text-sm">
            {{ t("pages.clubs.challenges.prize") }}: {{ prizeLabel(row) }}
          </p>
          <p class="text-xs text-muted-foreground">
            {{ t("pages.clubs.challenges.until") }}:
            {{ new Date(row.ends_at).toLocaleString() }}
          </p>
          <Button
            v-if="isOwner"
            size="sm"
            variant="outline"
            class="mt-3"
            :disabled="busy"
            @click="deactivate(row.id)"
          >
            {{ t("pages.clubs.challenges.deactivate") }}
          </Button>
        </article>
      </div>
      <p
        v-else-if="!loading"
        class="rounded-xl border border-border/70 bg-card/40 p-8 text-center text-sm text-muted-foreground"
      >
        {{ t("pages.clubs.challenges.empty") }}
      </p>
    </div>

    <div v-if="isOwner" class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
      <h3 class="text-sm font-semibold">
        {{ t("pages.clubs.challenges.pick_title") }}
      </h3>
      <p class="text-xs text-muted-foreground">
        {{ t("pages.clubs.challenges.pick_hint", { count: String(catalog.length) }) }}
      </p>
      <div class="flex flex-wrap gap-2">
        <Input
          v-model="search"
          class="min-w-[12rem] flex-1"
          :placeholder="t('pages.clubs.challenges.search')"
        />
        <select
          v-model="difficultyFilter"
          class="h-9 rounded-md border border-input bg-transparent px-2 text-sm"
        >
          <option value="">{{ t("pages.clubs.challenges.all_difficulties") }}</option>
          <option v-for="d in DIFFICULTIES" :key="d" :value="d">
            {{ t(`pages.clubs.challenges.difficulty.${d}`) }}
          </option>
        </select>
        <select
          v-model="categoryFilter"
          class="h-9 rounded-md border border-input bg-transparent px-2 text-sm"
        >
          <option value="">{{ t("pages.clubs.challenges.all_categories") }}</option>
          <option v-for="c in CATEGORIES" :key="c" :value="c">
            {{ t(`pages.clubs.challenges.category.${c}`) }}
          </option>
        </select>
      </div>
      <div class="max-h-[22rem] space-y-1 overflow-y-auto rounded-md border border-border/60 p-2">
        <label
          v-for="row in filteredCatalog"
          :key="row.id"
          class="flex cursor-pointer items-start gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-muted/40"
          :class="liveIds.has(row.id) ? 'opacity-50' : ''"
        >
          <input
            type="checkbox"
            class="mt-1"
            :checked="selected.includes(row.id)"
            :disabled="liveIds.has(row.id)"
            @change="toggle(row.id)"
          />
          <span class="min-w-0 flex-1">
            <span class="font-medium">{{ catalogTitle(row) }}</span>
            <span
              class="ml-2 rounded-full border px-1.5 py-0.5 text-[10px] uppercase"
              :class="diffClass(row.difficulty)"
            >
              {{ t(`pages.clubs.challenges.difficulty.${row.difficulty}`) }}
            </span>
            <span class="block text-xs text-muted-foreground">
              {{ catalogDesc(row) }}
            </span>
          </span>
        </label>
      </div>
      <div class="grid gap-3 sm:grid-cols-3">
        <label class="space-y-1 text-xs">
          <span>{{ t("pages.clubs.challenges.ends_at") }}</span>
          <Input v-model="endsAt" type="datetime-local" />
        </label>
        <label class="space-y-1 text-xs">
          <span>{{ t("pages.clubs.challenges.prize_ypoints") }}</span>
          <Input v-model.number="prizeYpoints" type="number" min="0" />
        </label>
        <label class="space-y-1 text-xs">
          <span>{{ t("pages.clubs.challenges.prize_other") }}</span>
          <Input v-model="prizeOther" />
        </label>
      </div>
      <Button
        size="sm"
        :class="tacticalCtaButtonClasses"
        :disabled="busy || !selected.length"
        @click="activate"
      >
        {{
          t("pages.clubs.challenges.activate", {
            count: String(selected.length),
          })
        }}
      </Button>
    </div>

    <div v-if="isOwner && past.length" class="space-y-2">
      <h3 class="text-sm font-semibold text-muted-foreground">
        {{ t("pages.clubs.challenges.past") }}
      </h3>
      <ul class="space-y-1 text-sm text-muted-foreground">
        <li v-for="row in past.slice(0, 12)" :key="row.id">
          {{ row.catalog ? catalogTitle(row.catalog) : row.catalog_id }}
          —
          {{ new Date(row.ends_at).toLocaleString() }}
        </li>
      </ul>
    </div>
  </div>
</template>
