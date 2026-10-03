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
