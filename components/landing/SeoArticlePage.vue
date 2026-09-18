<script setup lang="ts">
import { loginLinks } from "~/utilities/loginLinks";
import type { SeoPageKey } from "~/utils/siteSeo";

const props = defineProps<{
  page: SeoPageKey;
}>();

const { copy, isFa } = useSiteSeo(props.page);
const { brandName, logoUrl } = useBranding();
const displayBrand = computed(() => brandName.value || "YGuard");

useHead({
  link: [
    {
      rel: "stylesheet",
      href: "https://cdn.jsdelivr.net/npm/geist@1.3.1/dist/fonts/geist-sans/style.min.css",
    },
  ],
});

function login() {
  window.location.href = `${loginLinks.steam}?redirect=${encodeURIComponent(window.location.origin + "/play")}`;
}
</script>

<template>
  <article
    class="seo-article mx-auto min-h-svh max-w-3xl px-6 py-14 text-white"
    :dir="isFa ? 'rtl' : 'ltr'"
  >
    <header class="mb-10 space-y-4">
      <NuxtLink
        to="/"
        class="inline-flex items-center gap-2 text-sm font-bold uppercase tracking-wide text-white/50 hover:text-[#aa0e19]"
      >
        <img
          v-if="logoUrl"
          :src="logoUrl"
          :alt="displayBrand"
          class="h-7 w-7 object-contain"
        />
        {{ displayBrand }}
      </NuxtLink>
      <h1 class="m-0 text-[clamp(1.75rem,4vw,2.5rem)] font-black leading-tight">
        {{ copy.h1 }}
      </h1>
      <p class="m-0 text-lg leading-relaxed text-white/60">
        {{ copy.description }}
      </p>
    </header>

    <div class="prose-seo space-y-5 text-[1.05rem] leading-relaxed text-white/75">
      <slot />
    </div>

    <footer class="mt-12 flex flex-wrap items-center gap-4 border-t border-white/10 pt-8">
      <button
        type="button"
        class="rounded-[4px] bg-[#aa0e19] px-6 py-3 text-sm font-black uppercase tracking-wide text-[#060606] hover:brightness-110"
        @click="login"
      >
        {{ isFa ? "ورود با استیم و بازی" : "Login with Steam & play" }}
      </button>
      <NuxtLink to="/" class="text-sm font-bold text-white/50 hover:text-white">
        {{ isFa ? "بازگشت به صفحه اصلی" : "Back to home" }}
      </NuxtLink>
    </footer>
  </article>
</template>

<style scoped>
.seo-article {
  font-family: "Geist Sans", geistSans, ui-sans-serif, system-ui, sans-serif;
}
.prose-seo :deep(h2) {
  margin: 1.75rem 0 0.75rem;
  font-size: 1.25rem;
  font-weight: 800;
  color: #fff;
}
.prose-seo :deep(ul) {
  margin: 0.5rem 0 1rem;
  padding-inline-start: 1.25rem;
}
.prose-seo :deep(li) {
  margin: 0.35rem 0;
}
.prose-seo :deep(a) {
  color: #e85a5a;
  text-decoration: underline;
  text-underline-offset: 3px;
}
</style>
