<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from "vue";
import { ImagePlus, Send, X } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { compressNewsPhoto } from "~/utilities/imagePipeline";
import { resolveAvatarUrl } from "~/utilities/avatarUrl";
import { formatSiteDate } from "~/utilities/dateLocale";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";

type NewsAuthor = { name?: string | null; steam_id?: string | number | null };
type NewsPost = {
  id: string;
  body?: string | null;
  image_url?: string | null;
  created_at: string;
  deleted_at?: string | null;
  author_steam_id?: string | number | null;
  author?: NewsAuthor | null;
};

const props = defineProps<{
  posts: NewsPost[];
  clubName: string;
  apiDomain: string;
  canPost: boolean;
  canModerate: boolean;
  postingAsClub: boolean;
  isOwnerPoster: (msg: NewsPost) => boolean;
  busy?: boolean;
  asClubHint: string;
  asAccountHint: string;
  placeholder: string;
  staffOnly: string;
  empty: string;
  addImage: string;
  sendLabel: string;
  deleteLabel: string;
}>();

const emit = defineEmits<{
  send: [payload: { body: string; file: File | null }];
  delete: [id: string];
  openClub: [];
}>();

const body = ref("");
const file = ref<File | null>(null);
const preview = ref<string | null>(null);
const previewRatio = ref<string>("4 / 3");
const fileInput = ref<HTMLInputElement | null>(null);
const compressing = ref(false);
const lightbox = ref<string | null>(null);
const feedEl = ref<HTMLElement | null>(null);

const PHOTO_MAX_H = 420;

const sorted = computed(() =>
  [...props.posts].sort(
    (a, b) =>
      new Date(a.created_at).getTime() - new Date(b.created_at).getTime(),
  ),
);

function clearImage() {
  if (preview.value) URL.revokeObjectURL(preview.value);
  preview.value = null;
  file.value = null;
  previewRatio.value = "4 / 3";
  if (fileInput.value) fileInput.value.value = "";
}

async function onPick(event: Event) {
  const picked = (event.target as HTMLInputElement).files?.[0];
  if (!picked) return;
  if (
    !/^image\/(png|jpeg|webp)$/.test(picked.type) ||
    picked.size > 12 * 1024 * 1024
  ) {
    if (fileInput.value) fileInput.value.value = "";
    return;
  }
  compressing.value = true;
  try {
    clearImage();
    const compressed = await compressNewsPhoto(picked);
    file.value = compressed;
    preview.value = URL.createObjectURL(compressed);
    const img = new Image();
    img.onload = () => {
      if (img.naturalWidth && img.naturalHeight) {
        previewRatio.value = `${img.naturalWidth} / ${img.naturalHeight}`;
      }
    };
    img.src = preview.value;
  } finally {
    compressing.value = false;
  }
}

function submit() {
  const text = body.value.trim();
  if ((!text && !file.value) || props.busy || compressing.value) return;
  emit("send", { body: text, file: file.value });
}

function afterSent() {
  body.value = "";
  clearImage();
  void nextTick(() => {
    const el = feedEl.value;
    if (el) el.scrollTop = el.scrollHeight;
  });
}

defineExpose({ afterSent, clearImage });

watch(
  () => props.posts.length,
  async () => {
    await nextTick();
    const el = feedEl.value;
    if (el) el.scrollTop = el.scrollHeight;
  },
);

onBeforeUnmount(() => {
  if (preview.value) URL.revokeObjectURL(preview.value);
});

function photoStyle() {
  // Telegram: keep ratio, cap height so tall shots don't dominate.
  return {
    maxHeight: `${PHOTO_MAX_H}px`,
    width: "100%",
    maxWidth: "100%",
  } as Record<string, string>;
}

function timeLabel(value: string) {
  try {
    return formatSiteDate(value, { timeStyle: "short" });
  } catch {
    return value;
  }
}

function dateLabel(value: string) {
  try {
    return formatSiteDate(value, { dateStyle: "medium" });
  } catch {
    return value;
  }
}

const canSubmit = computed(
  () =>
    !props.busy &&
    !compressing.value &&
    (!!body.value.trim() || !!file.value),
);
</script>

<template>
  <section
    class="flex min-h-[28rem] flex-col overflow-hidden rounded-2xl border border-border/70 bg-[hsl(var(--card)/0.55)]"
  >
    <!-- Feed (Telegram channel style) -->
    <div
      ref="feedEl"
      class="flex-1 space-y-3 overflow-y-auto px-3 py-4 sm:px-4"
      style="max-height: min(70vh, 36rem)"
    >
      <p
        v-if="!sorted.length"
        class="rounded-xl border border-dashed border-border/50 px-4 py-10 text-center text-sm text-muted-foreground"
      >
        {{ empty }}
      </p>

      <article
        v-for="msg in sorted"
        :key="msg.id"
        class="mx-auto w-full max-w-[min(100%,26rem)]"
        :class="{ 'opacity-45': msg.deleted_at }"
      >
        <div
          class="overflow-hidden rounded-2xl border border-border/40 bg-[hsl(var(--background)/0.72)] shadow-sm"
        >
          <!-- Photo first (Telegram caption-under-photo) -->
          <button
            v-if="msg.image_url && !msg.deleted_at"
            type="button"
            class="group relative block w-full cursor-zoom-in bg-black/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--tac-amber))]"
            @click="
              lightbox = resolveAvatarUrl(msg.image_url, apiDomain) || null
            "
          >
            <img
              :src="resolveAvatarUrl(msg.image_url, apiDomain) || undefined"
              alt=""
              class="mx-auto block h-auto w-full object-contain"
              :style="photoStyle()"
              loading="lazy"
            />
          </button>

          <div class="space-y-1.5 px-3 py-2.5">
            <div class="flex items-baseline justify-between gap-2">
              <button
                v-if="isOwnerPoster(msg)"
                type="button"
                class="truncate text-[13px] font-semibold text-[hsl(var(--tac-amber))] hover:underline"
                @click="emit('openClub')"
              >
                {{ clubName }}
              </button>
              <span
                v-else
                class="truncate text-[13px] font-semibold text-[hsl(var(--tac-amber))]"
              >
                {{ msg.author?.name || msg.author_steam_id }}
              </span>
              <span
                class="shrink-0 font-mono text-[10px] tabular-nums text-muted-foreground"
                :title="dateLabel(msg.created_at)"
              >
                {{ timeLabel(msg.created_at) }}
              </span>
            </div>

            <p
              v-if="msg.body && msg.body.trim()"
              class="whitespace-pre-wrap text-[14px] leading-relaxed text-foreground"
              :class="{ 'line-through opacity-70': msg.deleted_at }"
            >
              {{ msg.body }}
            </p>

            <button
              v-if="canModerate && !msg.deleted_at"
              type="button"
              class="text-[11px] text-destructive/90 hover:underline"
              @click="emit('delete', msg.id)"
            >
              {{ deleteLabel }}
            </button>
          </div>
        </div>
      </article>
    </div>

    <!-- Composer -->
    <div
      v-if="canPost"
      class="border-t border-border/60 bg-[hsl(var(--card)/0.9)] px-3 py-3 backdrop-blur-sm"
    >
      <p class="mb-2 text-[11px] text-muted-foreground">
        {{ postingAsClub ? asClubHint : asAccountHint }}
      </p>

      <div
        v-if="preview"
        class="relative mb-2 overflow-hidden rounded-xl border border-border/50 bg-black/30"
      >
        <img
          :src="preview"
          alt=""
          class="mx-auto block h-auto max-h-52 w-auto max-w-full object-contain"
          :style="{ aspectRatio: previewRatio }"
        />
        <button
          type="button"
          class="absolute end-2 top-2 grid h-7 w-7 place-items-center rounded-full bg-black/65 text-white hover:bg-black/80"
          :aria-label="deleteLabel"
          @click="clearImage"
        >
          <X class="h-3.5 w-3.5" />
        </button>
      </div>

      <div class="flex items-end gap-2">
        <label
          class="grid h-10 w-10 shrink-0 cursor-pointer place-items-center rounded-full border border-border/70 bg-background/50 text-muted-foreground transition hover:text-[hsl(var(--tac-amber))]"
          :title="addImage"
        >
          <input
            ref="fileInput"
            type="file"
            accept="image/png,image/jpeg,image/webp"
            class="sr-only"
            @change="onPick"
          />
          <ImagePlus class="h-4 w-4" />
        </label>

        <textarea
          v-model="body"
          rows="1"
          class="max-h-28 min-h-10 flex-1 resize-none rounded-2xl border border-border/70 bg-background px-3 py-2.5 text-sm leading-snug outline-none focus:border-[hsl(var(--tac-amber)/0.55)]"
          :placeholder="placeholder"
          @keydown.enter.exact.prevent="submit"
        />

        <Button
          type="button"
          size="icon"
          class="h-10 w-10 shrink-0 rounded-full"
          :disabled="!canSubmit"
          :class="tacticalCtaButtonClasses"
          @click="submit"
        >
          <Send class="h-4 w-4" />
          <span class="sr-only">{{ sendLabel }}</span>
        </Button>
      </div>
      <p v-if="compressing" class="mt-1.5 text-[11px] text-muted-foreground">
        …
      </p>
    </div>
    <p v-else class="border-t border-border/60 px-4 py-3 text-sm text-muted-foreground">
      {{ staffOnly }}
    </p>

    <!-- Lightbox -->
    <Teleport to="body">
      <button
        v-if="lightbox"
        type="button"
        class="fixed inset-0 z-[80] flex cursor-zoom-out items-center justify-center bg-black/85 p-4"
        @click="lightbox = null"
      >
        <img
          :src="lightbox"
          alt=""
          class="max-h-[92vh] max-w-[min(100%,56rem)] object-contain"
          @click.stop
        />
      </button>
    </Teleport>
  </section>
</template>
