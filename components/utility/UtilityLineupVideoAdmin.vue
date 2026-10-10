<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Loader2, Trash2, Upload } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { toast } from "~/components/ui/toast";
import getGraphqlClient from "~/graphql/getGraphqlClient";
import { utilityLineupQuery } from "~/graphql/utilityGraphql";
import type { UtilityLineup } from "~/types/utility";

// The API takes one direct post, which Cloudflare caps at ~100MB.
const MAX_BYTES = 90 * 1024 * 1024;

const props = defineProps<{ lineup: UtilityLineup }>();

const emit = defineEmits<{
  (e: "updated", patch: Partial<UtilityLineup>): void;
}>();

const { t } = useI18n();

const input = ref<HTMLInputElement | null>(null);
const progress = ref<number | null>(null);
const removing = ref(false);

const hasVideo = computed(() => !!(props.lineup.preview_url ?? "").trim());
const busy = computed(() => progress.value !== null || removing.value);

function endpoint(): string {
  const apiDomain = useRuntimeConfig().public.apiDomain;
  return `https://${apiDomain}/utility/videos/${props.lineup.id}`;
}

function durationOf(file: File): Promise<number | null> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file);
    const video = document.createElement("video");
    const done = (value: number | null) => {
      URL.revokeObjectURL(url);
      resolve(value);
    };
    video.preload = "metadata";
    video.onloadedmetadata = () =>
      done(Number.isFinite(video.duration) ? video.duration * 1000 : null);
    video.onerror = () => done(null);
    video.src = url;
  });
}

function send(file: File, durationMs: number | null): Promise<void> {
  const form = new FormData();
  form.append("file", file, file.name || "lineup.mp4");
  if (durationMs) {
    form.append("duration_ms", String(Math.round(durationMs)));
  }

  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", endpoint());
    xhr.withCredentials = true;
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        progress.value = Math.round((event.loaded / event.total) * 100);
      }
    };
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve();
      } else {
        let message = `${xhr.status}`;
        try {
          message = JSON.parse(xhr.responseText)?.message ?? message;
        } catch {}
        reject(new Error(message));
      }
    };
    xhr.onerror = () => reject(new Error("network error"));
    xhr.send(form);
  });
}

// The URL carries the upload time as its cache-buster, so it has to be read
// back rather than guessed.
async function refresh() {
  const { data } = await getGraphqlClient().query({
    query: utilityLineupQuery(),
    variables: { id: props.lineup.id },
    fetchPolicy: "network-only",
  });
  const fresh = (data as any)?.utility_lineups_by_pk;
  emit("updated", {
    preview_url: fresh?.preview_url ?? null,
    preview_thumbnail_url: fresh?.preview_thumbnail_url ?? null,
    preview_duration_ms: fresh?.preview_duration_ms ?? null,
  });
}

async function onPick(event: Event) {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  target.value = "";
  if (!file) {
    return;
  }

  if (file.type !== "video/mp4") {
    toast({
      title: t("pages.utility.video.only_mp4"),
      variant: "destructive",
    });
    return;
  }

  if (file.size > MAX_BYTES) {
    toast({
      title: t("pages.utility.video.too_large", { mb: MAX_BYTES / 1024 / 1024 }),
      variant: "destructive",
    });
    return;
  }

  progress.value = 0;
  try {
    await send(file, await durationOf(file));
    await refresh();
    toast({ title: t("pages.utility.video.uploaded") });
  } catch (error) {
    toast({
      title: t("pages.utility.video.upload_failed"),
      description: (error as Error)?.message,
      variant: "destructive",
    });
  } finally {
    progress.value = null;
  }
}

async function remove() {
  removing.value = true;
  try {
    const response = await fetch(endpoint(), {
      method: "DELETE",
      credentials: "include",
    });
    if (!response.ok) {
      throw new Error(`${response.status} ${response.statusText}`);
    }
    await refresh();
    toast({ title: t("pages.utility.video.removed") });
  } catch (error) {
    toast({
      title: t("pages.utility.video.remove_failed"),
      description: (error as Error)?.message,
      variant: "destructive",
    });
  } finally {
    removing.value = false;
  }
}
</script>

<template>
  <div
    class="flex flex-wrap items-center gap-2 rounded-md border border-dashed border-border/70 px-3 py-2"
  >
    <span
      class="mr-auto font-mono text-[0.6rem] uppercase tracking-[0.16em] text-muted-foreground"
    >
      {{ $t("pages.utility.video.title") }}
    </span>

    <input
      ref="input"
      type="file"
      accept="video/mp4"
      class="hidden"
      @change="onPick"
    />

    <Button
      size="sm"
      variant="outline"
      :disabled="busy"
      @click="input?.click()"
    >
      <Loader2 v-if="progress !== null" class="h-3.5 w-3.5 animate-spin" />
      <Upload v-else class="h-3.5 w-3.5" />
      <template v-if="progress !== null">{{ progress }}%</template>
      <template v-else>
        {{
          hasVideo
            ? $t("pages.utility.video.replace")
            : $t("pages.utility.video.upload")
        }}
      </template>
    </Button>

    <Button
      v-if="hasVideo"
      size="sm"
      variant="ghost"
      class="text-destructive hover:text-destructive"
      :disabled="busy"
      @click="remove"
    >
      <Loader2 v-if="removing" class="h-3.5 w-3.5 animate-spin" />
      <Trash2 v-else class="h-3.5 w-3.5" />
      {{ $t("pages.utility.video.remove") }}
    </Button>
  </div>
</template>
