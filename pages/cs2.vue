<script setup lang="ts">
import SeoArticlePage from "~/components/landing/SeoArticlePage.vue";

definePageMeta({ layout: "landing" });
</script>

<template>
  <SeoArticlePage page="cs2">
    <p>
      وای گارد صف‌های رقابتی کانتر استرایک ۲ را با سیستم Elo واقعی برگزار
      می‌کند. چه سولو باشی چه با تیم پنج‌نفره، می‌توانی در Competitive،
      Wingman، Duel و حالت‌های دیگر رنک بگیری.
    </p>
    <h2>چرا مچ‌میکینگ CS2 در وای گارد؟</h2>
    <ul>
      <li>رنک سی اس مبتنی بر Elo و تاریخچه مچ</li>
      <li>سرورهای اختصاصی با پینگ مناسب بازیکن‌های ایران</li>
      <li>آنتی‌چیت اختصاصی برای بازی تمیز</li>
      <li>اسکرم، کاستوم لابی و تورنمنت در یک پلتفرم</li>
    </ul>
    <p>
      اگر دنبال «رنک CS2»، «مچمیکینگ سی اس» یا جایگزین جدی برای صف‌های رسمی
      هستی، از صفحه اصلی وای گارد با استیم وارد شو و Find Match بزن.
    </p>
    <p>
      بیشتر بدان:
      <NuxtLink to="/anticheat">آنتی‌چیت</NuxtLink>
      ·
      <NuxtLink to="/servers">سرورهای پابلیک</NuxtLink>
    </p>
  </SeoArticlePage>
</template>
