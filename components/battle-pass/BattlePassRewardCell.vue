<script setup lang="ts">
import { computed } from "vue";
import { Award, Check, Crown, Lock, Loader2, Zap } from "lucide-vue-next";
import type { BattlePassReward } from "./types";

const props = defineProps<{
  reward: BattlePassReward | null;
  premium?: boolean;
  reached: boolean;
  claimed: boolean;
  locked: boolean;
  busy?: boolean;
}>();

const emit = defineEmits<{ (e: "claim"): void }>();

const claimable = computed(
  () => !!props.reward && props.reached && !props.claimed && !props.locked,
);

function onClick() {
  if (claimable.value && !props.busy) {
    emit("claim");
  }
}
</script>

<template>
  <div
    v-if="!reward"
    class="h-[7.25rem] rounded-2xl border border-dashed border-white/[0.06] bg-black/25"
  />
  <button
    v-else
    type="button"
    class="group relative flex h-[7.25rem] w-full flex-col items-center justify-center gap-1 overflow-hidden rounded-2xl border px-1.5 text-center transition duration-200"
    :class="[
      claimable
        ? 'cursor-pointer border-[hsl(var(--tac-amber))] bg-[hsl(var(--tac-amber)/0.2)] shadow-[0_0_32px_hsla(var(--tac-amber)/0.35)] hover:scale-[1.03] hover:bg-[hsl(var(--tac-amber)/0.3)]'
        : claimed
          ? 'cursor-default border-emerald-400/50 bg-emerald-500/[0.14]'
          : premium
            ? 'cursor-default border-[hsl(var(--tac-amber)/0.4)] bg-gradient-to-b from-[hsl(var(--tac-amber)/0.18)] via-[#141821] to-[#0a0d12]'
            : 'cursor-default border-white/[0.1] bg-gradient-to-b from-[#161b24] to-[#0c1016]',
      !reached && !claimed ? 'opacity-45 grayscale-[0.35]' : '',
    ]"
    :title="claimable ? $t('pages.battle_pass.claim') : undefined"
    @click="onClick"
  >
    <div
      v-if="premium && !claimed"
      class="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-[hsl(var(--tac-amber)/0.7)] to-transparent"
      aria-hidden="true"
    />

    <template v-if="reward.type === 'ypoints'">
      <img
        src="/img/ypoint-logo.png"
        alt=""
        class="h-9 w-9 object-contain drop-shadow-[0_2px_8px_rgba(0,0,0,0.45)]"
      />
      <span class="text-[1.05rem] font-black tabular-nums tracking-tight text-white">{{
        reward.amount
      }}</span>
      <span
        class="text-[9px] font-bold uppercase tracking-[0.16em] text-white/40"
        >YP</span
      >
    </template>
    <template v-else-if="reward.type === 'xp'">
      <Zap class="h-9 w-9 text-[hsl(var(--tac-amber))] drop-shadow-[0_0_12px_hsla(var(--tac-amber)/0.55)]" />
      <span
        class="text-[1.05rem] font-black tabular-nums tracking-tight text-[hsl(var(--tac-amber))]"
        >+{{ reward.amount }}</span
      >
      <span
        class="text-[9px] font-bold uppercase tracking-[0.16em] text-white/40"
        >XP</span
      >
    </template>
    <template v-else-if="reward.type === 'subscription'">
      <Crown
        class="h-9 w-9 drop-shadow-[0_0_12px_hsla(var(--tac-amber)/0.45)]"
        :class="
          reward.tier === 'premium_plus'
            ? 'text-amber-200'
            : 'text-[hsl(var(--tac-amber))]'
        "
      />
      <span class="px-0.5 text-[10px] font-semibold leading-snug text-white/90">
        {{
          $t("pages.battle_pass.reward.subscription", {
            tier:
              reward.tier === "premium_plus"
                ? $t("pages.challenges.tier_plus")
                : $t("pages.challenges.tier_premium"),
            days: reward.days,
          })
        }}
      </span>
    </template>
    <template v-else>
      <Award class="h-9 w-9 text-[hsl(var(--tac-amber))]" />
      <span class="px-0.5 text-[10px] font-semibold leading-snug text-white/90">
        {{ $t(`pages.battle_pass.badge.${reward.badge}`) }}
      </span>
    </template>

    <span
      v-if="claimed"
      class="absolute right-1.5 top-1.5 rounded-full bg-emerald-500 p-0.5 text-white shadow-sm"
    >
      <Check class="h-3 w-3" />
    </span>
    <span
      v-else-if="locked || !reached"
      class="absolute right-1.5 top-1.5 rounded-full bg-black/40 p-0.5 text-white/40"
    >
      <Lock class="h-3.5 w-3.5" />
    </span>
    <span
      v-else-if="busy"
      class="absolute right-1.5 top-1.5 text-[hsl(var(--tac-amber))]"
    >
      <Loader2 class="h-3.5 w-3.5 animate-spin" />
    </span>
    <span
      v-else-if="claimable"
      class="absolute inset-x-2 bottom-1.5 rounded-full bg-[hsl(var(--tac-amber))] py-0.5 text-[9px] font-bold uppercase tracking-wider text-black"
    >
      {{ $t("pages.battle_pass.claim") }}
    </span>
  </button>
</template>
