export type BattlePassReward =
  | { type: "ypoints"; amount: number }
  | { type: "subscription"; tier: "premium" | "premium_plus"; days: number }
  | { type: "badge"; badge: "veteran" | "elite" | "legend" };
