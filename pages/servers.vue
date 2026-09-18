<script setup lang="ts">
import SeoArticlePage from "~/components/landing/SeoArticlePage.vue";

definePageMeta({ layout: "landing" });
</script>

<template>
  <SeoArticlePage page="servers">
    <p>
      وای گارد علاوه بر مچ‌میکینگ رنک، سرورهای پابلیک و Dedicated کانتر
      استرایک ۲ را هم میزبانی و مدیریت می‌کند — مناسب تمرین، پابلیک، و
      لابی‌های کاستوم.
    </p>
    <h2>چه چیزی می‌گیری؟</h2>
    <ul>
      <li>سرور پابلیک CS2 با جوین آسان از سایت</li>
      <li>سرورهای اختصاصی برای مچ رسمی و اسکرم</li>
      <li>همان آنتی‌چیت وای گارد روی پابلیک و کاستوم (در صورت فعال بودن)</li>
      <li>مدیریت نود، پلاگین و کانفیگ از پنل</li>
    </ul>
    <p>
      برای دیدن لیست سرورها بعد از لاگین به
      <strong>Public Servers</strong>
      برو، یا از صفحه اصلی وارد شو و بازی را شروع کن.
    </p>
    <p>
      مرتبط:
      <NuxtLink to="/cs2">رنک و مچ‌میکینگ</NuxtLink>
      ·
      <NuxtLink to="/anticheat">آنتی‌چیت</NuxtLink>
    </p>
  </SeoArticlePage>
</template>
