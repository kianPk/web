<script setup lang="ts">
import SeoArticlePage from "~/components/landing/SeoArticlePage.vue";

definePageMeta({ layout: "landing" });
</script>

<template>
  <SeoArticlePage page="anticheat">
    <p>
      آنتی‌چیت وای گارد (YGuard Anti-Cheat) لانچر ویندوزی است که برای مچ‌های
      رنک، سرور پابلیک و کاستوم CS2 اجباری می‌شود. هدف ساده است: بازی تمیز
      بدون چیت.
    </p>
    <h2>چی چک می‌شود؟</h2>
    <ul>
      <li>Secure Boot و TPM 2.0</li>
      <li>HVCI / Memory Integrity (اختیاری از سمت ادمین)</li>
      <li>اسکن امضای نرم‌افزارهای تقلب شناخته‌شده</li>
      <li>قلب زنده لانچر — بستن AC یعنی کیک از سرور</li>
    </ul>
    <p>
      دانلود از دکمه Install AntiCheat در سایت، یا صفحه
      <NuxtLink to="/ac">/ac</NuxtLink>.
      بعد از pair با استیم، لانچر را موقع صف و بازی باز نگه دار.
    </p>
    <p>
      مرتبط:
      <NuxtLink to="/cs2">مچ‌میکینگ CS2</NuxtLink>
      ·
      <NuxtLink to="/servers">سرورها</NuxtLink>
    </p>
  </SeoArticlePage>
</template>
