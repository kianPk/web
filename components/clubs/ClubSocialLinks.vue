<script setup lang="ts">
const props = defineProps<{
  telegram?: string | null;
  instagram?: string | null;
  bale?: string | null;
  rubika?: string | null;
  soroush?: string | null;
}>();

const links = computed(() =>
  [
    {
      key: "telegram",
      href: props.telegram,
      label: "pages.clubs.telegram",
      color: "#2AABEE",
    },
    {
      key: "instagram",
      href: props.instagram,
      label: "pages.clubs.instagram",
      color: "#E1306C",
    },
    {
      key: "bale",
      href: props.bale,
      label: "pages.clubs.bale",
      color: "#00A86B",
    },
    {
      key: "rubika",
      href: props.rubika,
      label: "pages.clubs.rubika",
      color: "#7B2FBE",
    },
    {
      key: "soroush",
      href: props.soroush,
      label: "pages.clubs.soroush",
      color: "#1E88E5",
    },
  ].filter((link) => !!link.href),
);
</script>

<template>
  <div v-if="links.length" class="flex flex-wrap items-center gap-2">
    <a
      v-for="link in links"
      :key="link.key"
      :href="link.href!"
      target="_blank"
      rel="noopener noreferrer"
      class="inline-flex items-center gap-1.5 rounded-full border bg-card/60 px-2.5 py-1 text-xs font-medium transition-opacity hover:opacity-80"
      :style="{ color: link.color, borderColor: link.color }"
    >
      <svg viewBox="0 0 24 24" class="h-3.5 w-3.5 fill-current" aria-hidden="true">
        <path
          v-if="link.key === 'telegram'"
          d="M21.5 4.3 18.6 20c-.2 1-.8 1.2-1.6.8l-4.5-3.3-2.2 2.1c-.2.3-.5.5-.9.5l.3-4.7 8.5-7.7c.4-.3 0-.5-.5-.2l-10.5 6.6-4.5-1.4c-1-.3-1-.9.2-1.4L20.2 3.5c.8-.3 1.5.2 1.3.8Z"
        />
        <path
          v-else-if="link.key === 'instagram'"
          d="M7 3h10a4 4 0 0 1 4 4v10a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V7a4 4 0 0 1 4-4Zm10 2H7a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2Zm-5 3.2A3.8 3.8 0 1 1 8.2 12 3.8 3.8 0 0 1 12 8.2Zm0 2A1.8 1.8 0 1 0 13.8 12 1.8 1.8 0 0 0 12 10.2ZM17.2 6.6a.9.9 0 1 1-.9.9.9.9 0 0 1 .9-.9Z"
        />
        <path
          v-else-if="link.key === 'bale'"
          d="M12 3.2c4.6 0 8.2 3.2 8.2 7.3 0 4-3.6 7.2-8.2 7.2-.7 0-1.4-.1-2-.2L5.2 20.2c-.4.2-.8-.3-.6-.7l1.2-3.1C4.1 15.1 3 12.9 3 10.5 3 6.4 7.4 3.2 12 3.2Z"
        />
        <path
          v-else-if="link.key === 'rubika'"
          d="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm-2.2 5.2a2.3 2.3 0 1 1 0 4.6 2.3 2.3 0 0 1 0-4.6Zm5.4 1.2a1.7 1.7 0 1 1 0 3.4 1.7 1.7 0 0 1 0-3.4ZM7.6 14.2c.7 1.6 2.4 2.7 4.4 2.7s3.7-1.1 4.4-2.7c.2-.4-.2-.8-.6-.6a6 6 0 0 1-7.6 0c-.4-.2-.8.2-.6.6Z"
        />
        <path
          v-else
          d="M12 3a9 9 0 1 0 .01 18.01A9 9 0 0 0 12 3Zm-1.1 5.2h4.4c.4 0 .7.4.5.8L13.2 13h2.2c.5 0 .7.6.4 1l-4.6 5.2c-.4.4-1 .1-.9-.4l.8-3.6H8.6c-.5 0-.7-.6-.4-1l2.2-4.4c.2-.3.4-.6.5-.6Z"
        />
      </svg>
      {{ $t(link.label) }}
    </a>
  </div>
</template>
