<script setup lang="ts">
import { Calendar as CalendarIcon } from "lucide-vue-next";
import { computed, ref, watch } from "vue";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import ShamsiDateTimePanel from "~/components/common/ShamsiDateTimePanel.vue";
import { formatShamsi } from "~/utilities/jalali";

const props = withDefaults(
  defineProps<{
    modelValue?: Date | string | null;
    disablePastDates?: boolean;
    disabled?: boolean;
  }>(),
  {
    modelValue: null,
    disablePastDates: false,
    disabled: false,
  },
);

const emit = defineEmits<{
  "update:modelValue": [value: Date];
}>();

const open = ref(false);

const label = computed(() => {
  if (!props.modelValue) return "";
  return formatShamsi(props.modelValue);
});

watch(
  () => props.disabled,
  (value) => {
    if (value) open.value = false;
  },
);

function onPick(date: Date) {
  emit("update:modelValue", date);
}
</script>

<template>
  <Popover v-model:open="open">
    <PopoverTrigger as-child :disabled="disabled">
      <Button
        type="button"
        variant="outline"
        class="w-full max-w-[22rem] justify-start text-left font-normal"
        :class="{ 'text-muted-foreground': !label }"
        :disabled="disabled"
      >
        <CalendarIcon class="mr-2 h-4 w-4 shrink-0" />
        <span class="truncate" dir="rtl">
          {{ label || $t("common.pick_date") }}
        </span>
      </Button>
    </PopoverTrigger>
    <PopoverContent class="w-auto border-border/70 p-0" align="start">
      <ShamsiDateTimePanel
        :model-value="modelValue"
        :disable-past-dates="disablePastDates"
        :disabled="disabled"
        @update:model-value="onPick"
      />
    </PopoverContent>
  </Popover>
</template>
