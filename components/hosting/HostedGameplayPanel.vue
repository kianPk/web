<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Crosshair, Footprints } from "lucide-vue-next";
import { Label } from "~/components/ui/label";
import { Switch } from "~/components/ui/switch";
import { toast } from "~/components/ui/toast";
import {
  hostedApi,
  hostedErrorMessage,
  type HostedGameplay,
  type HostedServer,
} from "~/composables/useHostedServers";

const props = defineProps<{
  hostedId: string;
  gameplay: HostedGameplay | null | undefined;
}>();

const emit = defineEmits<{
  updated: [server: HostedServer];
}>();

const { t } = useI18n();
const friendlyFire = ref(false);
const bunnyHop = ref(false);
const busy = ref<"ff" | "bhop" | null>(null);

watch(
  () => [props.hostedId, props.gameplay] as const,
  () => {
    friendlyFire.value = !!props.gameplay?.friendly_fire;
    bunnyHop.value = !!props.gameplay?.bunny_hop;
  },
  { immediate: true, deep: true },
);

async function setFlag(
  key: "friendly_fire" | "bunny_hop",
  value: boolean,
) {
  if (busy.value) return;
  const previous = {
    friendly_fire: friendlyFire.value,
    bunny_hop: bunnyHop.value,
  };
  if (key === "friendly_fire") friendlyFire.value = value;
  else bunnyHop.value = value;
  busy.value = key === "friendly_fire" ? "ff" : "bhop";
  try {
    const server = await hostedApi<HostedServer>(
      `/hosted-servers/${props.hostedId}/gameplay`,
      {
        method: "POST",
        body: { [key]: value },
      },
    );
    emit("updated", server);
    toast({
      title: t(
        key === "friendly_fire"
          ? value
            ? "pages.hosting.panel.gameplay.ff_on"
            : "pages.hosting.panel.gameplay.ff_off"
          : value
            ? "pages.hosting.panel.gameplay.bhop_on"
            : "pages.hosting.panel.gameplay.bhop_off",
      ),
    });
  } catch (error) {
    friendlyFire.value = previous.friendly_fire;
    bunnyHop.value = previous.bunny_hop;
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = null;
  }
}
</script>

<template>
  <section
    class="space-y-4 rounded-lg border border-border bg-card/40 p-4"
  >
    <div class="space-y-1">
      <h2 class="m-0 text-base font-semibold">
        {{ $t("pages.hosting.panel.gameplay.title") }}
      </h2>
      <p class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.panel.gameplay.hint") }}
      </p>
    </div>

    <div class="space-y-2">
      <div
        class="flex items-center justify-between gap-3 rounded-lg border border-border/70 bg-background/30 px-3 py-2.5"
      >
        <div class="flex min-w-0 items-center gap-2.5">
          <Crosshair class="h-4 w-4 shrink-0 text-muted-foreground" />
          <div class="min-w-0">
            <Label class="text-sm font-medium">
              {{ $t("pages.hosting.panel.gameplay.friendly_fire") }}
            </Label>
            <p class="m-0 text-[0.7rem] text-muted-foreground">
              {{ $t("pages.hosting.panel.gameplay.friendly_fire_hint") }}
            </p>
          </div>
        </div>
        <Switch
          :model-value="friendlyFire"
          :disabled="!!busy"
          @update:model-value="(v) => setFlag('friendly_fire', !!v)"
        />
      </div>

      <div
        class="flex items-center justify-between gap-3 rounded-lg border border-border/70 bg-background/30 px-3 py-2.5"
      >
        <div class="flex min-w-0 items-center gap-2.5">
          <Footprints class="h-4 w-4 shrink-0 text-muted-foreground" />
          <div class="min-w-0">
            <Label class="text-sm font-medium">
              {{ $t("pages.hosting.panel.gameplay.bunny_hop") }}
            </Label>
            <p class="m-0 text-[0.7rem] text-muted-foreground">
              {{ $t("pages.hosting.panel.gameplay.bunny_hop_hint") }}
            </p>
          </div>
        </div>
        <Switch
          :model-value="bunnyHop"
          :disabled="!!busy"
          @update:model-value="(v) => setFlag('bunny_hop', !!v)"
        />
      </div>
    </div>
  </section>
</template>
