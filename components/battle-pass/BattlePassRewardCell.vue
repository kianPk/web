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
    class="h-28 rounded-lg border border-dashed border-white/10 bg-black/20"
  />
  <button
    v-else
    type="button"
    class="relative flex h-28 w-full flex-col items-center justify-center gap-1.5 rounded-lg border px-1.5 text-center transition-all duration-200"
    :class="[
      claimable
        ? 'cursor-pointer border-[#e3d39a] bg-[#e3d39a]/15 shadow-[0_0_20px_rgba(227,211,154,0.35)] hover:bg-[#e3d39a]/25'
        : claimed
          ? 'cursor-default border-emerald-500/40 bg-emerald-500/10'
          : premium
            ? 'cursor-default border-amber-500/25 bg-gradient-to-b from-amber-500/10 to-transparent'
            : 'cursor-default border-white/10 bg-white/[0.03]',
      !reached && !claimed ? 'opacity-50' : '',
    ]"
    :title="claimable ? $t('pages.battle_pass.claim') : undefined"
    @click="onClick"
  >
    <template v-if="reward.type === 'ypoints'">
      <img src="/img/ypoint-logo.png" alt="" class="h-8 w-8 object-contain" />
      <span class="text-sm font-bold tabular-nums">{{ reward.amount }}</span>
      <span class="text-[10px] uppercase tracking-wider text-muted-foreground"
        >YP</span
      >
    </template>
    <template v-else-if="reward.type === 'xp'">
      <Zap class="h-8 w-8 text-[#e3d39a]" />
      <span class="text-sm font-bold tabular-nums text-[#e3d39a]"
        >+{{ reward.amount }}</span
      >
      <span class="text-[10px] uppercase tracking-wider text-muted-foreground"
        >XP</span
      >
    </template>
    <template v-else-if="reward.type === 'subscription'">
      <Crown
        class="h-8 w-8"
        :class="
          reward.tier === 'premium_plus' ? 'text-violet-400' : 'text-amber-400'
        "
      />
      <span class="text-[11px] font-semibold leading-tight">
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
        class="h-8 w-8"
        :class="
          reward.badge === 'legend'
            ? 'text-violet-400'
            : reward.badge === 'champion' || reward.badge === 'elite'
              ? 'text-amber-400'
              : 'text-sky-400'
        "
      />
      <span class="text-[11px] font-semibold leading-tight">
        {{ $t(`pages.battle_pass.badge.${reward.badge}`) }}
      </span>
    </template>

    <span
      v-if="claimed"
      class="absolute right-1.5 top-1.5 rounded-full bg-emerald-500/90 p-0.5 text-white"
    >
      <Check class="h-3 w-3" />
    </span>
    <span
      v-else-if="locked || !reached"
      class="absolute right-1.5 top-1.5 text-muted-foreground/80"
    >
      <Lock class="h-3.5 w-3.5" />
    </span>
    <span
      v-else-if="busy"
      class="absolute right-1.5 top-1.5 text-[#e3d39a]"
    >
      <Loader2 class="h-3.5 w-3.5 animate-spin" />
    </span>
  </button>
</template>
