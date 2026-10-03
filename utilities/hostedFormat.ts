type Translate = (key: string, params?: Record<string, unknown>) => string;

export function formatHostedDuration(duration: string, t: Translate): string {
  const m = (duration || "")
    .trim()
    .toLowerCase()
    .match(/^(\d+)\s*(h|hr|hrs|d|day|days|w|week|weeks|mo|month|months)$/);
  if (!m) {
    return duration;
  }
  const n = Number(m[1]);
  const unit = m[2];
  if (/^(h|hr|hrs)$/.test(unit))
    return t("pages.hosting.duration.hours", { n });
  if (/^(d|day|days)$/.test(unit))
    return t("pages.hosting.duration.days", { n });
  if (/^(w|week|weeks)$/.test(unit))
    return t("pages.hosting.duration.weeks", { n });
  return t("pages.hosting.duration.months", { n });
}

/** Must match HostedServersService.durationMs on the API (unknown = 30 days). */
export function hostedDurationDays(duration: string): number {
  const m = (duration || "")
    .trim()
    .toLowerCase()
    .match(/^(\d+)\s*(h|hr|hrs|d|day|days|w|week|weeks|mo|month|months)$/);
  if (!m) {
    return 30;
  }
  const n = Number(m[1]);
  const unit = m[2];
  const days = /^(h|hr|hrs)$/.test(unit)
    ? n / 24
    : /^(d|day|days)$/.test(unit)
      ? n
      : /^(w|week|weeks)$/.test(unit)
        ? n * 7
        : n * 30;
  return Math.max(1, Math.round(days));
}

/** Price of `slots` extra slots for `days`, same rounding as the API. */
export function hostedSlotsCost(
  pricing: { slot_price_irr: number; slot_price_ypoint: number },
  slots: number,
  days: number,
): { irr: number; ypoint: number } {
  if (!slots) {
    return { irr: 0, ypoint: 0 };
  }
  return {
    irr:
      Math.ceil((pricing.slot_price_irr * slots * days) / 30 / 10_000) * 10_000,
    ypoint:
      pricing.slot_price_ypoint > 0
        ? Math.ceil((pricing.slot_price_ypoint * slots * days) / 30)
        : 0,
  };
}

export function formatHostedDate(value: string, locale: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString(locale?.startsWith("fa") ? "fa-IR" : "en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export function hostedStatusVariant(
  status: string,
): "default" | "secondary" | "destructive" | "outline" {
  switch (status) {
    case "active":
      return "default";
    case "provisioning":
      return "secondary";
    case "expired":
    case "failed":
    case "suspended":
      return "destructive";
    default:
      return "outline";
  }
}
