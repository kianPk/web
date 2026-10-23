<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { ChevronLeft, ChevronRight } from "lucide-vue-next";
import { Button } from "~/components/ui/button";
import {
  dateFromShamsi,
  daysInJalaliMonth,
  formatShamsi,
  gregorianToJalali,
  JALALI_WEEKDAYS,
  jalaliMonthName,
  jalaliWeekday,
  shamsiParts,
} from "~/utilities/jalali";

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

function partsFromValue(value: Date | string | null | undefined) {
  const date =
    value == null || value === ""
      ? new Date()
      : value instanceof Date
        ? value
        : new Date(value);
  if (Number.isNaN(date.getTime())) return shamsiParts(new Date());
  return shamsiParts(date);
}

const seed = partsFromValue(props.modelValue);
const viewY = ref(Number(seed.y));
const viewM = ref(Number(seed.m));
const selectedY = ref(Number(seed.y));
const selectedM = ref(Number(seed.m));
const selectedD = ref(Number(seed.d));
const hour = ref(seed.hh);
const minute = ref(seed.mm);
let syncing = false;

watch(
  () => props.modelValue,
  (value) => {
    if (value == null || value === "") return;
    const next = partsFromValue(value);
    syncing = true;
    selectedY.value = Number(next.y);
    selectedM.value = Number(next.m);
    selectedD.value = Number(next.d);
    hour.value = next.hh;
    minute.value = next.mm;
    viewY.value = Number(next.y);
    viewM.value = Number(next.m);
    queueMicrotask(() => {
      syncing = false;
    });
  },
);

const monthLabel = computed(
  () => `${jalaliMonthName(viewM.value)} ${viewY.value}`,
);

const cells = computed(() => {
  const days = daysInJalaliMonth(viewY.value, viewM.value);
  const offset = jalaliWeekday(viewY.value, viewM.value, 1);
  const today = gregorianToJalali(
    new Date().getFullYear(),
    new Date().getMonth() + 1,
    new Date().getDate(),
  );
  const list: Array<{
    day: number | null;
    disabled: boolean;
    selected: boolean;
    today: boolean;
  }> = [];
  for (let i = 0; i < offset; i++) {
    list.push({ day: null, disabled: true, selected: false, today: false });
  }
  for (let day = 1; day <= days; day++) {
    const past =
      props.disablePastDates &&
      (viewY.value < today.jy ||
        (viewY.value === today.jy && viewM.value < today.jm) ||
        (viewY.value === today.jy &&
          viewM.value === today.jm &&
          day < today.jd));
    list.push({
      day,
      disabled: past,
      selected:
        selectedY.value === viewY.value &&
        selectedM.value === viewM.value &&
        selectedD.value === day,
      today:
        today.jy === viewY.value &&
        today.jm === viewM.value &&
        today.jd === day,
    });
  }
  return list;
});

const preview = computed(() => {
  const date = dateFromShamsi({
    y: selectedY.value,
    m: selectedM.value,
    d: selectedD.value,
    hh: hour.value,
    mm: minute.value,
  });
  return date ? formatShamsi(date) : "";
});

function emitValue() {
  if (syncing || props.disabled) return;
  const date = dateFromShamsi({
    y: selectedY.value,
    m: selectedM.value,
    d: selectedD.value,
    hh: hour.value,
    mm: minute.value,
  });
  if (!date) return;
  if (props.disablePastDates && date.getTime() <= Date.now()) return;
  emit("update:modelValue", date);
}

function pickDay(day: number, disabled: boolean) {
  if (disabled || props.disabled) return;
  selectedY.value = viewY.value;
  selectedM.value = viewM.value;
  selectedD.value = day;
  emitValue();
}

function shiftMonth(delta: number) {
  if (props.disabled) return;
  let m = viewM.value + delta;
  let y = viewY.value;
  if (m < 1) {
    m = 12;
    y -= 1;
  } else if (m > 12) {
    m = 1;
    y += 1;
  }
  viewY.value = y;
  viewM.value = m;
}

watch([hour, minute], () => emitValue());
</script>

<template>
  <div
    class="w-[min(100%,19rem)] space-y-3 rounded-xl border border-border/70 bg-card/80 p-3"
    :class="{ 'pointer-events-none opacity-60': disabled }"
  >
    <div class="flex items-center justify-between gap-2">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        class="h-8 w-8"
        :disabled="disabled"
        @click="shiftMonth(-1)"
      >
        <ChevronRight class="h-4 w-4" />
      </Button>
      <p class="m-0 text-sm font-semibold tracking-tight">{{ monthLabel }}</p>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        class="h-8 w-8"
        :disabled="disabled"
        @click="shiftMonth(1)"
      >
        <ChevronLeft class="h-4 w-4" />
      </Button>
    </div>

    <div class="grid grid-cols-7 gap-1 text-center" dir="rtl">
      <span
        v-for="name in JALALI_WEEKDAYS"
        :key="name"
        class="py-1 text-[0.65rem] font-medium text-muted-foreground"
      >
        {{ name }}
      </span>
      <button
        v-for="(cell, index) in cells"
        :key="index"
        type="button"
        class="h-8 rounded-md text-sm tabular-nums transition-colors"
        :class="
          !cell.day
            ? 'invisible'
            : cell.selected
              ? 'bg-[hsl(var(--tac-amber))] text-[hsl(var(--tac-amber-foreground))]'
              : cell.disabled
                ? 'cursor-not-allowed text-muted-foreground/40'
                : cell.today
                  ? 'bg-[hsl(var(--tac-amber)/0.12)] text-foreground hover:bg-[hsl(var(--tac-amber)/0.2)]'
                  : 'hover:bg-muted/60'
        "
        :disabled="!cell.day || cell.disabled || disabled"
        @click="cell.day && pickDay(cell.day, cell.disabled)"
      >
        {{ cell.day || "" }}
      </button>
    </div>

    <div class="flex items-center gap-2 border-t border-border/60 pt-3" dir="ltr">
      <label class="flex min-w-0 flex-1 flex-col gap-1 text-[0.65rem] text-muted-foreground">
        {{ $t("common.hour") }}
        <select
          v-model="hour"
          class="h-9 rounded-md border border-input bg-transparent px-2 font-mono text-sm text-foreground"
          :disabled="disabled"
        >
          <option
            v-for="h in 24"
            :key="h"
            :value="String(h - 1).padStart(2, '0')"
          >
            {{ String(h - 1).padStart(2, "0") }}
          </option>
        </select>
      </label>
      <span class="pt-4 text-muted-foreground">:</span>
      <label class="flex min-w-0 flex-1 flex-col gap-1 text-[0.65rem] text-muted-foreground">
        {{ $t("common.minute") }}
        <select
          v-model="minute"
          class="h-9 rounded-md border border-input bg-transparent px-2 font-mono text-sm text-foreground"
          :disabled="disabled"
        >
          <option
            v-for="m in 60"
            :key="m"
            :value="String(m - 1).padStart(2, '0')"
          >
            {{ String(m - 1).padStart(2, "0") }}
          </option>
        </select>
      </label>
    </div>

    <p
      v-if="preview"
      class="m-0 rounded-md bg-muted/40 px-2.5 py-1.5 text-center text-xs text-muted-foreground"
      dir="rtl"
    >
      {{ preview }}
    </p>
  </div>
</template>
