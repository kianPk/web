export const OWN_CLUB_TAG = "OwnClub";

export function clubErrorMessage(
  raw: unknown,
  t: (key: string, params?: Record<string, string>) => string,
): string {
  const msg =
    (raw as { graphQLErrors?: Array<{ message?: string }> })?.graphQLErrors?.[0]
      ?.message ||
    (raw as Error)?.message ||
    String(raw || "");
  if (msg.includes("OwnClub tag required")) {
    return t("pages.clubs.ownclub_required");
  }
  if (msg.includes("Transfer the club before leaving")) {
    return t("pages.clubs.transfer_before_leave");
  }
  const until = msg.match(/Cannot join a club until (.+)$/i);
  if (until?.[1]) {
    const parsed = new Date(until[1].trim());
    return t("pages.clubs.cooldown_until", {
      when: Number.isNaN(parsed.getTime())
        ? until[1].trim()
        : parsed.toLocaleString(),
    });
  }
  if (msg.includes("Cannot join a club")) {
    return t("pages.clubs.cooldown_locked");
  }
  return msg || t("pages.clubs.action_failed");
}
