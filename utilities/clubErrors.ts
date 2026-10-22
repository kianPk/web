import { formatSiteDate } from "~/utilities/dateLocale";

export const OWN_CLUB_TAG = "OwnClub";

function collectErrorText(raw: unknown): string {
  const parts: string[] = [];
  const seen = new Set<unknown>();
  const walk = (value: unknown) => {
    if (value == null || seen.has(value)) return;
    if (typeof value === "string") {
      parts.push(value);
      return;
    }
    if (typeof value !== "object") return;
    seen.add(value);
    const rec = value as Record<string, unknown>;
    if (typeof rec.message === "string") parts.push(rec.message);
    if (Array.isArray(rec.graphQLErrors)) rec.graphQLErrors.forEach(walk);
    walk(rec.extensions);
    walk(rec.internal);
    walk(rec.error);
  };
  walk(raw);
  return parts.join("\n");
}

export function clubErrorMessage(
  raw: unknown,
  t: (key: string, params?: Record<string, string>) => string,
): string {
  const msg = collectErrorText(raw) || String(raw || "");
  if (msg.includes("OwnClub tag required") || msg.includes("own_club_tag_required")) {
    return t("pages.clubs.ownclub_required");
  }
  if (msg.includes("Transfer the club before leaving")) {
    return t("pages.clubs.transfer_before_leave");
  }
  const until =
    msg.match(/Cannot join a club until (.+)$/im) ||
    msg.match(/club_join_cooldown:([^\s\n]+(?:\s[^\s\n]+)?)/);
  if (until?.[1]) {
    const parsed = new Date(until[1].trim());
    return t("pages.clubs.cooldown_until", {
      when: Number.isNaN(parsed.getTime())
        ? until[1].trim()
        : formatSiteDate(parsed),
    });
  }
  if (msg.includes("Cannot join a club") || msg.includes("club_join_cooldown")) {
    return t("pages.clubs.cooldown_locked");
  }
  if (
    msg.includes("Join request already pending") ||
    msg.includes("club_join_requests_club_id_steam_id_key")
  ) {
    return t("pages.clubs.already_requested");
  }
  if (msg.includes("Already a club member")) {
    return t("pages.clubs.already_member");
  }
  if (msg.includes("Only the club owner or an admin can post news")) {
    return t("pages.clubs.news_staff_only");
  }
  if (msg.includes("Only the club owner can activate challenges")) {
    return t("pages.clubs.challenges.owner_only");
  }
  if (msg.includes("Challenge end time must be in the future")) {
    return t("pages.clubs.challenges.need_future_end");
  }
  if (msg.includes("Challenge needs a YPoint prize")) {
    return t("pages.clubs.challenges.need_prize");
  }
  if (
    msg.includes("club_challenges_one_active_catalog") ||
    msg.includes("already active")
  ) {
    return t("pages.clubs.challenges.already_active");
  }
  if (msg.includes("database query error")) {
    return t("pages.clubs.action_failed");
  }
  return msg || t("pages.clubs.action_failed");
}
