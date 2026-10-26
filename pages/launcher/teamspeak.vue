<script setup lang="ts">
import { Headset, Copy, ExternalLink } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import { toast } from "~/components/ui/toast";

definePageMeta({ layout: "launcher" });
const { t } = useI18n();
useHead({ title: "TeamSpeak" });

const address = "yguard.asts.ir:6857";
const connectUrl = "ts3server://yguard.asts.ir?port=6857";

async function copyAddress() {
  try {
    await navigator.clipboard.writeText(address);
    toast({ title: t("layouts.launcher.teamspeak_copied") });
  } catch {
    toast({
      title: t("layouts.launcher.teamspeak_copy_failed"),
      variant: "destructive",
    });
  }
}
</script>

<template>
  <div class="absolute inset-0 flex items-center justify-center overflow-auto p-6 sm:p-10">
    <div
      class="w-full max-w-lg rounded-3xl border border-white/10 bg-white/[0.04] p-7 text-center shadow-[0_24px_80px_rgba(0,0,0,0.45)] backdrop-blur-xl sm:p-10"
    >
      <div
        class="mx-auto mb-5 grid h-16 w-16 place-items-center rounded-2xl bg-[#ff5500]/15 text-[#ff5500] ring-1 ring-[#ff5500]/25"
      >
        <Headset class="h-8 w-8" :stroke-width="1.75" />
      </div>
      <h1 class="text-xl font-semibold tracking-tight text-white">
        {{ t("layouts.app_nav.navigation.teamspeak") }}
      </h1>
      <p class="mt-2 text-sm text-white/50">
        {{ t("layouts.launcher.teamspeak_hint") }}
      </p>
      <p
        class="mt-6 rounded-xl bg-black/35 px-4 py-3 font-mono text-sm tracking-wide text-[#ff6a1f] ring-1 ring-white/5"
      >
        {{ address }}
      </p>
      <div class="mt-6 flex flex-wrap items-center justify-center gap-2">
        <Button class="bg-[#ff5500] text-white hover:bg-[#ff6a1f]" as-child>
          <a :href="connectUrl">
            <ExternalLink class="h-4 w-4" />
            {{ t("layouts.launcher.teamspeak_connect") }}
          </a>
        </Button>
        <Button
          variant="outline"
          class="border-white/10 bg-transparent text-white/80 hover:bg-white/[0.06]"
          @click="copyAddress"
        >
          <Copy class="h-4 w-4" />
          {{ t("layouts.launcher.teamspeak_copy") }}
        </Button>
      </div>
    </div>
  </div>
</template>
