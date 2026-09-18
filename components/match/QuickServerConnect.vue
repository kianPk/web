<script lang="ts">
import { ExternalLink, Copy, ShieldAlert } from "lucide-vue-next";
import { Button } from "@/components/ui/button";
import ClipBoard from "~/components/ClipBoard.vue";
import { toast } from "@/components/ui/toast";

export default {
  components: {
    ExternalLink,
    Copy,
    ShieldAlert,
    ClipBoard,
    Button,
  },
  props: {
    server: {
      type: Object,
      required: true,
    },
    highlight: {
      type: Boolean,
      default: false,
    },
  },
  data() {
    return {
      isLoading: false,
      acRequired: false,
      acValid: true,
      _acTimer: 0 as number,
    };
  },
  mounted() {
    void this.refreshAc();
    this._acTimer = window.setInterval(() => void this.refreshAc(), 10_000);
  },
  beforeUnmount() {
    if (this._acTimer) window.clearInterval(this._acTimer);
  },
  computed: {
    blockedByAc() {
      return this.acRequired && !this.acValid;
    },
    me() {
      return useAuthStore().me;
    },
  },
  methods: {
    async refreshAc() {
      if (!this.me?.steam_id) {
        this.acRequired = false;
        this.acValid = true;
        return;
      }
      try {
        const apiDomain = useRuntimeConfig().public.apiDomain as string;
        const status = await $fetch<{ required?: boolean; valid?: boolean }>(
          `https://${apiDomain}/plugins/ac/status`,
          { credentials: "include" },
        );
        this.acRequired = !!status?.required;
        this.acValid = !!status?.valid;
      } catch {
        this.acRequired = false;
        this.acValid = true;
      }
    },
    async handleClick(e: Event) {
      await this.refreshAc();
      if (this.blockedByAc) {
        e.preventDefault();
        toast({
          title: this.$t("ac.title"),
          description: this.$t("ac.join_need_ac"),
          variant: "destructive",
        });
        return;
      }
      this.isLoading = true;
      setTimeout(() => {
        this.isLoading = false;
      }, 10000);
    },
  },
};
</script>

<template>
  <div v-if="server.connection_string" class="flex items-center gap-1">
    <template v-if="blockedByAc">
      <Button
        :variant="highlight ? 'default' : 'outline'"
        class="min-w-32"
        :class="highlight ? 'tac-amber-cta' : ''"
        @click="handleClick"
      >
        <ShieldAlert class="w-4 h-4 mr-1" />
        {{ $t("ac.open_launcher") }}
      </Button>
    </template>
    <template v-else>
      <ClipBoard :data="server.connection_string"></ClipBoard>
      <a
        :href="server.connection_link"
        v-if="server.connection_link"
        @click="handleClick"
      >
        <Button
          :variant="highlight ? 'default' : 'outline'"
          class="min-w-32"
          :class="highlight ? 'tac-amber-cta' : ''"
          :loading="isLoading"
        >
          <ExternalLink class="w-4 h-4 mr-1" />
          {{ $t("server.join_server") }}
        </Button>
      </a>
    </template>
  </div>
</template>
