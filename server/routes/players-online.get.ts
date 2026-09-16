// Same-origin proxy for the guest landing online counter.
// Avoids CORS/credential quirks when the browser calls the API directly.
export default defineEventHandler(async () => {
  const config = useRuntimeConfig();
  const apiDomain =
    process.env.NUXT_PUBLIC_API_DOMAIN ||
    (config.public?.apiDomain as string | undefined);

  if (!apiDomain) {
    return { count: 0 };
  }

  try {
    const data = await $fetch<{ count?: number }>(
      `https://${apiDomain}/sockets/players-online`,
      { timeout: 4000 },
    );
    return { count: Number(data?.count) || 0 };
  } catch {
    return { count: 0 };
  }
});
