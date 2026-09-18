<script setup lang="ts">
import { onMounted, ref, computed } from "vue";
import { useRoute } from "vue-router";
import { useAuthStore } from "~/stores/AuthStore";
import { loginLinks } from "~/utilities/loginLinks";
import { Button } from "~/components/ui/button";
import { Shield, CheckCircle2, Loader2 } from "lucide-vue-next";

const route = useRoute();
const auth = useAuthStore();
const apiDomain = useRuntimeConfig().public.apiDomain as string;

const code = computed(() => String(route.query.code || "").toUpperCase());
const state = ref<"loading" | "need_login" | "ok" | "error">("loading");
const errorMsg = ref("");

async function approve() {
  if (!code.value) {
    state.value = "error";
    errorMsg.value = "Missing code";
    return;
  }
  if (!auth.me?.steam_id) {
    state.value = "need_login";
    return;
  }
  try {
    await $fetch(`https://${apiDomain}/plugins/ac/device/approve`, {
      method: "POST",
      credentials: "include",
      body: { code: code.value },
    });
    state.value = "ok";
  } catch (e: any) {
    state.value = "error";
    errorMsg.value =
      e?.data?.message || e?.statusMessage || "Could not link launcher";
  }
}

function loginSteam() {
  const next = `${window.location.origin}/ac/authorize?code=${encodeURIComponent(code.value)}`;
  window.location.href = `${loginLinks.steam}?redirect=${encodeURIComponent(next)}`;
}

onMounted(async () => {
  // Wait a tick for auth store
  await new Promise((r) => setTimeout(r, 200));
  if (!auth.me?.steam_id) {
    state.value = "need_login";
    return;
  }
  await approve();
});
</script>

<template>
  <div
    class="flex min-h-[70vh] items-center justify-center px-4 py-16"
  >
    <div
      class="w-full max-w-md space-y-6 rounded-xl border border-border bg-muted/20 p-8 text-center"
    >
      <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-[hsl(var(--tac-amber)/0.15)] text-[hsl(var(--tac-amber))]">
        <Shield class="h-6 w-6" />
      </div>
      <h1 class="text-xl font-semibold">YGuard Anti-Cheat</h1>

      <template v-if="state === 'loading'">
        <Loader2 class="mx-auto h-6 w-6 animate-spin text-muted-foreground" />
        <p class="text-sm text-muted-foreground">Linking launcher…</p>
      </template>

      <template v-else-if="state === 'need_login'">
        <p class="text-sm text-muted-foreground">
          Log in with the Steam account you use on yguard, then we’ll connect the launcher.
        </p>
        <Button class="w-full" @click="loginSteam">
          Login with Steam
        </Button>
      </template>

      <template v-else-if="state === 'ok'">
        <CheckCircle2 class="mx-auto h-10 w-10 text-emerald-500" />
        <p class="text-sm">
          Linked as <strong>{{ auth.me?.name || auth.me?.steam_id }}</strong>
        </p>
        <p class="text-xs text-muted-foreground">
          Return to the YGuard Anti-Cheat window — it will finish automatically.
        </p>
      </template>

      <template v-else>
        <p class="text-sm text-destructive">{{ errorMsg }}</p>
        <Button variant="outline" @click="approve">Retry</Button>
      </template>
    </div>
  </div>
</template>
