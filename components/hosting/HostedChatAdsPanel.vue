<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Megaphone, Plus, Trash2 } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { Checkbox } from "~/components/ui/checkbox";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { toast } from "~/components/ui/toast";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedChatAds,
  type HostedServer,
} from "~/composables/useHostedServers";

type MessageRow = { id: string; text: string };

const props = defineProps<{
  hostedId: string;
  ads: HostedChatAds | null | undefined;
}>();

const emit = defineEmits<{
  updated: [server: HostedServer];
}>();

const { t } = useI18n();
const busy = ref(false);
const enabled = ref(false);
const intervalSeconds = ref(120);
const color = ref("gold");
const messages = ref<MessageRow[]>([{ id: crypto.randomUUID(), text: "" }]);
const dirty = ref(false);

const INTERVALS = [30, 60, 90, 120, 180, 300, 600] as const;
const COLORS = [
  "gold",
  "green",
  "blue",
  "red",
  "purple",
  "lightred",
  "white",
  "grey",
] as const;

const PLACEHOLDERS = [
  "{time}",
  "{date}",
  "{map}",
  "{players}",
  "{maxplayers}",
  "{player}",
  "{online}",
] as const;

function toRows(lines: string[] | undefined): MessageRow[] {
  if (!lines?.length) return [{ id: crypto.randomUUID(), text: "" }];
  return lines.map((text) => ({ id: crypto.randomUUID(), text }));
}

function applyAds(ads: HostedChatAds | null | undefined) {
  enabled.value = !!ads?.enabled;
  intervalSeconds.value = ads?.interval_seconds || 120;
  color.value = ads?.color || "gold";
  messages.value = toRows(ads?.messages);
  dirty.value = false;
}

watch(
  () => props.hostedId,
  () => {
    applyAds(props.ads);
  },
  { immediate: true },
);

// Only re-sync from the server when the user is not mid-edit.
watch(
  () => props.ads,
  (ads) => {
    if (dirty.value) return;
    applyAds(ads);
  },
);

const canAdd = computed(() => messages.value.length < 5);

function markDirty() {
  dirty.value = true;
}

function addMessage() {
  if (!canAdd.value) return;
  messages.value = [
    ...messages.value,
    { id: crypto.randomUUID(), text: "" },
  ];
  markDirty();
}

function removeMessage(id: string) {
  const next = messages.value.filter((row) => row.id !== id);
  messages.value = next.length
    ? next
    : [{ id: crypto.randomUUID(), text: "" }];
  markDirty();
}

function setMessageText(id: string, text: string) {
  messages.value = messages.value.map((row) =>
    row.id === id ? { ...row, text: text.slice(0, 220) } : row,
  );
  markDirty();
}

function insertToken(token: string) {
  const last = messages.value[messages.value.length - 1];
  if (!last) return;
  const current = last.text || "";
  const next = `${current}${current && !current.endsWith(" ") ? " " : ""}${token}`;
  setMessageText(last.id, next);
}

async function save() {
  if (busy.value) return;
  busy.value = true;
  try {
    const server = await hostedApi<HostedServer>(
      `/hosted-servers/${props.hostedId}/chat-ads`,
      {
        method: "POST",
        body: {
          enabled: enabled.value,
          interval_seconds: intervalSeconds.value,
          color: color.value,
          messages: messages.value.map((row) => row.text),
        },
      },
    );
    emit("updated", server);
    applyAds(server.chat_ads);
    toast({ title: String(t("pages.hosting.panel.chat_ads.saved")) });
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <section class="hosted-card space-y-5 p-5 sm:p-6">
    <div class="flex items-start justify-between gap-3">
      <div>
        <h2 class="m-0 flex items-center gap-2 text-base font-semibold">
          <Megaphone class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
          {{ $t("pages.hosting.panel.chat_ads.title") }}
        </h2>
        <p class="mt-1 mb-0 text-xs text-muted-foreground">
          {{ $t("pages.hosting.panel.chat_ads.hint") }}
        </p>
      </div>
      <label class="flex cursor-pointer items-center gap-2 text-sm shrink-0">
        <Checkbox
          :model-value="enabled"
          @update:model-value="
            (v) => {
              enabled = !!v;
              markDirty();
            }
          "
        />
        {{ $t("pages.hosting.panel.chat_ads.enabled") }}
      </label>
    </div>

    <div class="space-y-1.5">
      <Label>{{ $t("pages.hosting.panel.chat_ads.interval") }}</Label>
      <div class="flex flex-wrap gap-1.5">
        <Button
          v-for="sec in INTERVALS"
          :key="sec"
          type="button"
          size="sm"
          :variant="intervalSeconds === sec ? 'default' : 'outline'"
          class="font-mono"
          @click="
            intervalSeconds = sec;
            markDirty();
          "
        >
          {{
            sec < 60
              ? $t("pages.hosting.panel.chat_ads.seconds", { n: sec })
              : $t("pages.hosting.panel.chat_ads.minutes", {
                  n: Math.round(sec / 60),
                })
          }}
        </Button>
      </div>
    </div>

    <div class="space-y-1.5">
      <Label>{{ $t("pages.hosting.panel.chat_ads.color") }}</Label>
      <div class="flex flex-wrap gap-1.5">
        <Button
          v-for="c in COLORS"
          :key="c"
          type="button"
          size="sm"
          :variant="color === c ? 'default' : 'outline'"
          @click="
            color = c;
            markDirty();
          "
        >
          {{ $t(`pages.hosting.panel.chat_ads.colors.${c}`) }}
        </Button>
      </div>
    </div>

    <div class="space-y-2">
      <Label>{{ $t("pages.hosting.panel.chat_ads.messages") }}</Label>
      <div
        v-for="(row, index) in messages"
        :key="row.id"
        class="flex items-center gap-2"
      >
        <Input
          :model-value="row.text"
          maxlength="220"
          dir="auto"
          :placeholder="
            $t('pages.hosting.panel.chat_ads.message_placeholder', {
              n: index + 1,
            })
          "
          @update:model-value="(v) => setMessageText(row.id, String(v ?? ''))"
        />
        <Button
          type="button"
          size="icon"
          variant="ghost"
          class="shrink-0"
          :disabled="messages.length <= 1"
          @click="removeMessage(row.id)"
        >
          <Trash2 class="h-4 w-4" />
        </Button>
      </div>
      <div class="flex flex-wrap gap-1.5">
        <Button
          v-for="token in PLACEHOLDERS"
          :key="token"
          type="button"
          size="sm"
          variant="outline"
          class="font-mono text-[0.7rem]"
          @click="insertToken(token)"
        >
          {{ token }}
        </Button>
      </div>
      <p class="m-0 text-[0.7rem] text-muted-foreground">
        {{ $t("pages.hosting.panel.chat_ads.placeholders_hint") }}
      </p>
      <Button
        type="button"
        size="sm"
        variant="outline"
        :disabled="!canAdd"
        @click="addMessage"
      >
        <Plus class="me-1.5 h-3.5 w-3.5" />
        {{ $t("pages.hosting.panel.chat_ads.add") }}
      </Button>
    </div>

    <Button size="sm" :disabled="busy" @click="save">
      {{ $t("pages.hosting.panel.save") }}
    </Button>
  </section>
</template>
