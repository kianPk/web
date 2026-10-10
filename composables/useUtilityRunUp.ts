import { ref } from "vue";
import type { UtilityRunUp } from "~/utilities/utilityThrowGuide";

// The run-up is read from the approach a preview render records, and this api
// films no previews, so there is never one to show.
export function useUtilityRunUp(_lineupId: () => string | null | undefined) {
  return ref<UtilityRunUp | null>(null);
}
