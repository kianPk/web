export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig();
  const query = getQuery(event);
  const limit = Number(query.limit) || 25;
  const apiDomain = config.public.apiDomain as string;
  return await $fetch(
    `https://${apiDomain}/hosted-servers/ranks/leaderboard`,
    { query: { limit } },
  );
});
