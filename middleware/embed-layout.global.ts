export default defineNuxtRouteMiddleware((to) => {
  if (to.query.embed === "1" || to.query.embed === "true") {
    setPageLayout("embed");
  }
});
