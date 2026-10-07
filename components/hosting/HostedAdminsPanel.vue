<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Ban, Crown, ShieldCheck, Trash2, UserPlus } from "lucide-vue-next";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { toast } from "~/components/ui/toast";
import { useAuthStore } from "~/stores/AuthStore";
import { formatHostedDate } from "~/utilities/hostedFormat";
import { hostedApi, hostedErrorMessage } from "~/composables/useHostedServers";

type HostedAdmin = {
  steam_id: string;
  name: string | null;
  avatar_url: string | null;
};

type HostedBan = {
  steam_id: string;
  name: string;
  reason: string;
  banned_by_name: string;
  expires_at: string | null;
};

const props = defineProps<{ hostedId: string }>();

const { t, locale } = useI18n();
const auth = useAuthStore();

const ownerSteamId = ref("");
const admins = ref<HostedAdmin[]>([]);
const bans = ref<HostedBan[]>([]);
const newAdmin = ref("");
const busy = ref(false);

const canManageAdmins = computed(() => {
  if (auth.isAdmin) return true;
  return String(ownerSteamId.value) === String(auth.me?.steam_id || "");
});

const COMMANDS = [
  "!slay",
  "!slap",
  "!kick",
  "!ban",
  "!bany",
  "!respawn",
] as const;

async function load() {
  try {
    const [adminList, banList] = await Promise.all([
      hostedApi<{ owner_steam_id: string; admins: HostedAdmin[] }>(
        `/hosted-servers/${props.hostedId}/admins`,
      ),
      hostedApi<HostedBan[]>(`/hosted-servers/${props.hostedId}/bans`),
    ]);
    ownerSteamId.value = adminList.owner_steam_id;
    admins.value = adminList.admins;
    bans.value = banList;
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  }
}

async function run(action: () => Promise<void>, successTitle: string) {
  if (busy.value) return;
  busy.value = true;
  try {
    await action();
    toast({ title: successTitle });
  } catch (error) {
    toast({ variant: "destructive", title: hostedErrorMessage(error) });
  } finally {
    busy.value = false;
  }
}

function addAdmin() {
  const steamId = newAdmin.value.trim();
  if (!steamId) return;
  return run(async () => {
    const result = await hostedApi<{ admins: HostedAdmin[] }>(
      `/hosted-servers/${props.hostedId}/admins`,
      { method: "POST", body: { steam_id: steamId } },
    );
    admins.value = result.admins;
    newAdmin.value = "";
  }, t("pages.hosting.admins.added"));
}

function removeAdmin(admin: HostedAdmin) {
  return run(async () => {
    const result = await hostedApi<{ admins: HostedAdmin[] }>(
      `/hosted-servers/${props.hostedId}/admins/${admin.steam_id}/delete`,
      { method: "POST" },
    );
    admins.value = result.admins;
  }, t("pages.hosting.admins.removed"));
}

function unban(ban: HostedBan) {
  return run(async () => {
    bans.value = await hostedApi<HostedBan[]>(
      `/hosted-servers/${props.hostedId}/bans/${ban.steam_id}/delete`,
      { method: "POST" },
    );
  }, t("pages.hosting.admins.unbanned"));
}

onMounted(load);
</script>

<template>
  <section class="hosted-card space-y-5 p-5 sm:p-6">
    <div class="space-y-1">
      <h2 class="m-0 flex items-center gap-2 text-base font-semibold">
        <ShieldCheck class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
        {{ $t("pages.hosting.admins.title") }}
      </h2>
      <p class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.admins.description") }}
      </p>
      <details class="group pt-1">
        <summary
          class="cursor-pointer list-none text-[0.7rem] font-medium uppercase tracking-wider text-muted-foreground transition-colors hover:text-foreground [&::-webkit-details-marker]:hidden"
        >
          {{ $t("pages.hosting.admins.commands_toggle") }}
        </summary>
        <div class="mt-2 flex flex-wrap gap-1.5" dir="ltr">
          <Badge
            v-for="command in COMMANDS"
            :key="command"
            variant="outline"
            class="font-mono text-[0.7rem]"
          >
            {{ command }}
          </Badge>
        </div>
      </details>
    </div>

    <form
      v-if="canManageAdmins"
      class="flex gap-2"
      @submit.prevent="addAdmin"
    >
      <Input
        v-model="newAdmin"
        dir="ltr"
        class="font-mono"
        maxlength="120"
        :placeholder="$t('pages.hosting.admins.placeholder')"
      />
      <Button type="submit" size="sm" :disabled="busy || !newAdmin.trim()">
        <UserPlus class="me-1.5 h-3.5 w-3.5" />
        {{ $t("pages.hosting.admins.add") }}
      </Button>
    </form>

    <ul class="m-0 list-none space-y-1.5 p-0">
      <li
        v-if="ownerSteamId"
        class="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm"
      >
        <Crown class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
        <span class="flex-1">
          {{
            String(ownerSteamId) === String(auth.me?.steam_id || "")
              ? $t("pages.hosting.admins.owner")
              : $t("pages.hosting.admins.owner_label")
          }}
        </span>
        <code class="text-xs text-muted-foreground" dir="ltr">
          {{ ownerSteamId }}
        </code>
      </li>
      <li
        v-for="admin in admins"
        :key="admin.steam_id"
        class="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm"
      >
        <img
          v-if="admin.avatar_url"
          :src="admin.avatar_url"
          alt=""
          class="h-6 w-6 rounded-full"
        />
        <span class="min-w-0 flex-1 truncate">
          {{ admin.name || $t("pages.hosting.admins.unknown_player") }}
        </span>
        <code class="text-xs text-muted-foreground" dir="ltr">
          {{ admin.steam_id }}
        </code>
        <Button
          v-if="canManageAdmins"
          size="icon"
          variant="ghost"
          class="h-7 w-7"
          :disabled="busy"
          :title="$t('pages.hosting.admins.remove')"
          @click="removeAdmin(admin)"
        >
          <Trash2 class="h-3.5 w-3.5" />
        </Button>
      </li>
    </ul>

    <div class="space-y-2">
      <h3 class="m-0 flex items-center gap-2 text-sm font-semibold">
        <Ban class="h-4 w-4" />
        {{ $t("pages.hosting.admins.bans_title", { n: bans.length }) }}
      </h3>
      <p v-if="!bans.length" class="m-0 text-xs text-muted-foreground">
        {{ $t("pages.hosting.admins.no_bans") }}
      </p>
      <ul v-else class="m-0 list-none space-y-1.5 p-0">
        <li
          v-for="ban in bans"
          :key="ban.steam_id"
          class="flex flex-wrap items-center gap-2 rounded-md border border-border px-3 py-2 text-sm"
        >
          <span class="min-w-0 flex-1 truncate">
            {{ ban.name || ban.steam_id }}
            <span v-if="ban.reason" class="text-muted-foreground">
              · {{ ban.reason }}
            </span>
          </span>
          <span class="text-xs text-muted-foreground">
            {{
              ban.expires_at
                ? $t("pages.hosting.admins.ban_until", {
                    date: formatHostedDate(ban.expires_at, locale),
                  })
                : $t("pages.hosting.admins.ban_permanent")
            }}
          </span>
          <Button
            size="sm"
            variant="outline"
            :disabled="busy"
            @click="unban(ban)"
          >
            {{ $t("pages.hosting.admins.unban") }}
          </Button>
        </li>
      </ul>
    </div>
  </section>
</template>
