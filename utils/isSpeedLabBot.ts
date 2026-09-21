/** User-Agents used by lab / audit crawlers that need a real first paint. */
export function isSpeedLabBot(ua: string | null | undefined): boolean {
  if (!ua) return false;
  return /Chrome-Lighthouse|PageSpeed|Lighthouse|GTmetrix|Pingdom|Speed Insights|WebPageTest/i.test(
    ua,
  );
}
