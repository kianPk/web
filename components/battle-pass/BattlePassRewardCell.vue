<script setup lang="ts">
import { computed } from "vue";
import { Award, Check, Crown, Lock, Loader2 } from "lucide-vue-next";
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
    class="h-24 rounded-md border border-dashed border-border/40"
  />
  <button
    v-else
    type="button"
    class="relative flex h-24 w-full flex-col items-center justify-center gap-1 rounded-md border px-1 text-center transition-colors"
    :class="[
      claimable
        ? 'cursor-pointer border-[hsl(var(--tac-amber))] bg-[hsl(var(--tac-amber)/0.12)] shadow-[0_0_14px_hsl(var(--tac-amber)/0.35)] hover:bg-[hsl(var(--tac-amber)/0.2)]'
        : claimed
          ? 'cursor-default border-emerald-500/40 bg-emerald-500/5'
          : premium
            ? 'cursor-default border-violet-500/30 bg-violet-500/5'
            : 'cursor-default border-border/60 bg-muted/20',
      !reached && !claimed ? 'opacity-60' : '',
    ]"
    :title="claimable ? $t('pages.battle_pass.claim') : undefined"
    @click="onClick"
  >
    <template v-if="reward.type === 'ypoints'">
      <img src="/img/ypoint-logo.png" alt="" class="h-7 w-7 object-contain" />
      <span class="text-sm font-semibold tabular-nums">
        {{ reward.amount }}
      </span>
    </template>
    <template v-else-if="reward.type === 'subscription'">
      <Crown
        class="h-7 w-7"
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
        class="h-7 w-7"
        :class="
          reward.badge === 'legend'
            ? 'text-violet-400'
            : reward.badge === 'elite'
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
      class="absolute right-1 top-1 rounded-full bg-emerald-500/90 p-0.5 text-white"
    >
      <Check class="h-3 w-3" />
    </span>
    <span
      v-else-if="locked || !reached"
      class="absolute right-1 top-1 text-muted-foreground"
    >
      <Lock class="h-3 w-3" />
    </span>
    <span
      v-else-if="busy"
      class="absolute right-1 top-1 text-[hsl(var(--tac-amber))]"
    >
      <Loader2 class="h-3 w-3 animate-spin" />
    </span>
  </button>
</template>
