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
    class="h-[7.25rem] rounded-xl border border-dashed border-white/[0.08] bg-black/30"
  />
  <button
    v-else
    type="button"
    class="group relative flex h-[7.25rem] w-full flex-col items-center justify-center gap-1 overflow-hidden rounded-xl border px-1.5 text-center transition-all duration-200"
    :class="[
      claimable
        ? premium
          ? 'cursor-pointer border-rose-400 bg-rose-500/20 shadow-[0_0_24px_rgba(244,63,94,0.35)] hover:bg-rose-500/30'
          : 'cursor-pointer border-emerald-400 bg-emerald-500/15 shadow-[0_0_24px_rgba(52,211,153,0.3)] hover:bg-emerald-500/25'
        : claimed
          ? 'cursor-default border-emerald-500/50 bg-emerald-500/10'
          : premium
            ? 'cursor-default border-rose-500/30 bg-gradient-to-b from-rose-500/15 via-[#1a0a0e] to-[#0c0e12]'
            : 'cursor-default border-white/10 bg-gradient-to-b from-white/[0.06] to-[#0c0e12]',
      !reached && !claimed ? 'opacity-45' : '',
    ]"
    :title="claimable ? $t('pages.battle_pass.claim') : undefined"
    @click="onClick"
  >
    <div
      class="pointer-events-none absolute inset-x-0 top-0 h-px"
      :class="premium ? 'bg-gradient-to-r from-transparent via-rose-400/70 to-transparent' : 'bg-gradient-to-r from-transparent via-white/20 to-transparent'"
      aria-hidden="true"
    />

    <template v-if="reward.type === 'ypoints'">
      <img src="/img/ypoint-logo.png" alt="" class="h-9 w-9 object-contain drop-shadow" />
      <span class="text-base font-black tabular-nums text-white">{{
        reward.amount
      }}</span>
      <span class="text-[10px] font-semibold uppercase tracking-[0.16em] text-white/50"
        >YP</span
      >
    </template>
    <template v-else-if="reward.type === 'xp'">
      <Zap class="h-9 w-9 text-amber-300 drop-shadow" />
      <span class="text-base font-black tabular-nums text-amber-300"
        >+{{ reward.amount }}</span
      >
      <span class="text-[10px] font-semibold uppercase tracking-[0.16em] text-white/50"
        >XP</span
      >
    </template>
    <template v-else-if="reward.type === 'subscription'">
      <Crown
        class="h-9 w-9 drop-shadow"
        :class="
          reward.tier === 'premium_plus' ? 'text-violet-300' : 'text-amber-300'
        "
      />
      <span class="px-0.5 text-[11px] font-bold leading-tight text-white">
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
      <Award
        class="h-9 w-9 drop-shadow"
        :class="
          reward.badge === 'legend'
            ? 'text-violet-300'
            : reward.badge === 'champion' || reward.badge === 'elite'
              ? 'text-amber-300'
              : 'text-sky-300'
        "
      />
      <span class="px-0.5 text-[11px] font-bold leading-tight text-white">
        {{ $t(`pages.battle_pass.badge.${reward.badge}`) }}
      </span>
    </template>

    <span
      v-if="claimed"
      class="absolute right-1.5 top-1.5 rounded-full bg-emerald-500 p-0.5 text-white shadow"
    >
      <Check class="h-3 w-3" />
    </span>
    <span
      v-else-if="locked || !reached"
      class="absolute right-1.5 top-1.5 text-white/40"
    >
      <Lock class="h-3.5 w-3.5" />
    </span>
    <span
      v-else-if="busy"
      class="absolute right-1.5 top-1.5 text-white"
    >
      <Loader2 class="h-3.5 w-3.5 animate-spin" />
    </span>
  </button>
</template>
