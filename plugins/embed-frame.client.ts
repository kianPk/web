export default defineNuxtPlugin(() => {
  if (!import.meta.client) return;

  const route = useRoute();
  const apply = () => {
    const embedded =
      route.query.embed === "1" || route.query.embed === "true";
    document.documentElement.classList.toggle("yguard-embed", embedded);
    document.body.classList.toggle("yguard-embed", embedded);
  };

  apply();
  watch(() => route.query.embed, apply);
});
