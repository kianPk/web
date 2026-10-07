<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { Crosshair, Footprints, Umbrella } from "lucide-vue-next";
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
const parachute = ref(false);
const busy = ref<"ff" | "bhop" | "chute" | null>(null);

type FlagKey = "friendly_fire" | "bunny_hop" | "parachute";

watch(
  () => [props.hostedId, props.gameplay] as const,
  () => {
    friendlyFire.value = !!props.gameplay?.friendly_fire;
    bunnyHop.value = !!props.gameplay?.bunny_hop;
    parachute.value = !!props.gameplay?.parachute;
  },
  { immediate: true, deep: true },
);

async function setFlag(key: FlagKey, value: boolean) {
  if (busy.value) return;
  const previous = {
    friendly_fire: friendlyFire.value,
    bunny_hop: bunnyHop.value,
    parachute: parachute.value,
  };
  if (key === "friendly_fire") friendlyFire.value = value;
  else if (key === "bunny_hop") bunnyHop.value = value;
  else parachute.value = value;
  busy.value =
    key === "friendly_fire" ? "ff" : key === "bunny_hop" ? "bhop" : "chute";
  try {
    const server = await hostedApi<HostedServer>(
      `/hosted-servers/${props.hostedId}/gameplay`,
      {
        method: "POST",
        body: { [key]: value },
      },
    );
    emit("updated", server);
    const toastKey =
      key === "friendly_fire"
        ? value
          ? "pages.hosting.panel.gameplay.ff_on"
          : "pages.hosting.panel.gameplay.ff_off"
        : key === "bunny_hop"
          ? value
            ? "pages.hosting.panel.gameplay.bhop_on"
            : "pages.hosting.panel.gameplay.bhop_off"
          : value
            ? "pages.hosting.panel.gameplay.parachute_on"
            : "pages.hosting.panel.gameplay.parachute_off";
    toast({ title: t(toastKey) });
  } catch (error) {
    friendlyFire.value = previous.friendly_fire;
    bunnyHop.value = previous.bunny_hop;
    parachute.value = previous.parachute;
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = null;
  }
}
</script>

<template>
  <section class="hosted-card space-y-5 p-5 sm:p-6">
    <div class="space-y-1">
      <h2
        class="m-0 inline-flex items-center gap-2 text-base font-semibold tracking-tight"
      >
        <span class="h-[2px] w-2.5 bg-[hsl(var(--tac-amber))]" />
        {{ $t("pages.hosting.panel.gameplay.title") }}
      </h2>
      <p class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.panel.gameplay.hint") }}
      </p>
    </div>

    <div class="space-y-2.5">
      <div
        class="flex items-center justify-between gap-3 rounded-xl border px-3.5 py-3 transition-colors"
        :class="
          friendlyFire
            ? 'border-[hsl(var(--tac-amber)/0.4)] bg-[hsl(var(--tac-amber)/0.08)]'
            : 'border-white/[0.07] bg-black/20'
        "
      >
        <div class="flex min-w-0 items-center gap-3">
          <span
            class="grid h-9 w-9 shrink-0 place-items-center rounded-lg border"
            :class="
              friendlyFire
                ? 'border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.15)] text-[hsl(var(--tac-amber))]'
                : 'border-white/10 bg-white/[0.03] text-muted-foreground'
            "
          >
            <Crosshair class="h-4 w-4" />
          </span>
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
        class="flex items-center justify-between gap-3 rounded-xl border px-3.5 py-3 transition-colors"
        :class="
          bunnyHop
            ? 'border-[hsl(var(--tac-amber)/0.4)] bg-[hsl(var(--tac-amber)/0.08)]'
            : 'border-white/[0.07] bg-black/20'
        "
      >
        <div class="flex min-w-0 items-center gap-3">
          <span
            class="grid h-9 w-9 shrink-0 place-items-center rounded-lg border"
            :class="
              bunnyHop
                ? 'border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.15)] text-[hsl(var(--tac-amber))]'
                : 'border-white/10 bg-white/[0.03] text-muted-foreground'
            "
          >
            <Footprints class="h-4 w-4" />
          </span>
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

      <div
        class="flex items-center justify-between gap-3 rounded-xl border px-3.5 py-3 transition-colors"
        :class="
          parachute
            ? 'border-[hsl(var(--tac-amber)/0.4)] bg-[hsl(var(--tac-amber)/0.08)]'
            : 'border-white/[0.07] bg-black/20'
        "
      >
        <div class="flex min-w-0 items-center gap-3">
          <span
            class="grid h-9 w-9 shrink-0 place-items-center rounded-lg border"
            :class="
              parachute
                ? 'border-[hsl(var(--tac-amber)/0.45)] bg-[hsl(var(--tac-amber)/0.15)] text-[hsl(var(--tac-amber))]'
                : 'border-white/10 bg-white/[0.03] text-muted-foreground'
            "
          >
            <Umbrella class="h-4 w-4" />
          </span>
          <div class="min-w-0">
            <Label class="text-sm font-medium">
              {{ $t("pages.hosting.panel.gameplay.parachute") }}
            </Label>
            <p class="m-0 text-[0.7rem] text-muted-foreground">
              {{ $t("pages.hosting.panel.gameplay.parachute_hint") }}
            </p>
          </div>
        </div>
        <Switch
          :model-value="parachute"
          :disabled="!!busy"
          @update:model-value="(v) => setFlag('parachute', !!v)"
        />
      </div>
    </div>
  </section>
</template>
