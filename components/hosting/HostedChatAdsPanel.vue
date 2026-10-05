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
const messages = ref<string[]>([""]);

const INTERVALS = [30, 60, 90, 120, 180, 300, 600] as const;

watch(
  () => props.ads,
  (ads) => {
    enabled.value = !!ads?.enabled;
    intervalSeconds.value = ads?.interval_seconds || 120;
    messages.value = ads?.messages?.length ? [...ads.messages] : [""];
  },
  { immediate: true },
);

const canAdd = computed(() => messages.value.length < 5);

function addMessage() {
  if (!canAdd.value) return;
  messages.value = [...messages.value, ""];
}

function removeMessage(index: number) {
  const next = messages.value.filter((_, i) => i !== index);
  messages.value = next.length ? next : [""];
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
          messages: messages.value,
        },
      },
    );
    emit("updated", server);
    toast({ title: String(t("pages.hosting.panel.chat_ads.saved")) });
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <section class="space-y-4 rounded-lg border border-border bg-card/40 p-4">
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
          @update:model-value="(v) => (enabled = !!v)"
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
          @click="intervalSeconds = sec"
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

    <div class="space-y-2">
      <Label>{{ $t("pages.hosting.panel.chat_ads.messages") }}</Label>
      <div
        v-for="(_, index) in messages"
        :key="index"
        class="flex items-center gap-2"
      >
        <Input
          v-model="messages[index]"
          maxlength="180"
          dir="auto"
          :placeholder="
            $t('pages.hosting.panel.chat_ads.message_placeholder', {
              n: index + 1,
            })
          "
        />
        <Button
          type="button"
          size="icon"
          variant="ghost"
          class="shrink-0"
          :disabled="messages.length <= 1"
          @click="removeMessage(index)"
        >
          <Trash2 class="h-4 w-4" />
        </Button>
      </div>
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
