<script setup lang="ts">
import MapDisplay from "~/components/MapDisplay.vue";
import SettingsSaveBar from "~/components/settings/SettingsSaveBar.vue";
import { FormSection } from "~/components/ui/form";
</script>

<template>
  <Form :form="form" @submit="saveMapPool">
    <FormSection :title="$t('pages.map_pools.title')">
      <FormField name="map_pool">
        <FormItem>
          <div class="space-y-6">
            <template
              v-for="(maps, type) in {
                [$t('maps.official')]: sortedMaps.official,
                [$t('maps.workshop')]: sortedMaps.workshop,
              }"
              :key="type"
            >
              <div v-if="maps && maps.length > 0">
                <Separator
                  v-if="type === 'Workshop Maps'"
                  class="text-2xl font-bold mb-4 text-center my-8"
                  :label="type"
                ></Separator>
                <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                  <template v-for="map in maps" :key="map.id">
                    <div
                      class="relative rounded-lg overflow-hidden transition-all duration-200 ease-in-out"
                      @click="updateMapPool(map.id)"
                      :class="{
                        'opacity-40': !form.values.map_pool?.includes(map.id),
                        'cursor-pointer transform hover:scale-105': true,
                      }"
                    >
                      <MapDisplay class="h-[150px]" :map="map">
                        <template v-slot:default v-if="map.active_pool">
                          <div class="absolute bottom-1">
                            <Badge variant="secondary" class="text-xs">{{
                              $t("maps.active_duty")
                            }}</Badge>
                          </div>
                        </template>
                      </MapDisplay>
                      <div
                        class="absolute inset-0 flex items-center justify-center bg-opacity-40 transition-opacity duration-200"
                      ></div>
                    </div>
                  </template>
                </div>
              </div>
            </template>
          </div>

          <FormMessage />
        </FormItem>
      </FormField>
    </FormSection>
    <div class="pb-24"></div>

    <SettingsSaveBar
      :dirty="isDirty && (form.values.map_pool?.length ?? 0) > 0"
      :submitting="submitting"
      @save="saveMapPool"
      @discard="discardChanges"
    />
  </Form>
</template>

<script lang="ts">
import { useForm } from "vee-validate";
import { toTypedSchema } from "~/utilities/vee-validate-zod";
import { z } from "zod";
import { generateMutation, generateQuery } from "~/graphql/graphqlGen";
import { order_by } from "~/generated/zeus";
import { toast } from "@/components/ui/toast";

interface Map {
  id: string;
  workshop_map_id?: string;
  active_pool?: boolean;
}

export default {
  props: {
    pool: {
      type: Object,
      required: true,
    },
    availableMaps: {
      type: Array as () => Map[],
      required: true,
    },
  },
  data() {
    return {
      submitting: false,
      baseline: null as string | null,
      isDirty: false,
      form: useForm({
        validationSchema: toTypedSchema(
          z.object({
            map_pool: z.array(z.string()),
          }),
        ),
      }),
    };
  },
  watch: {
    pool: {
      immediate: true,
      handler() {
        if (this.baseline === null || !this.isDirty) {
          this.populateForm();
        }
      },
    },
    ["form.values"]: {
      deep: true,
      handler() {
        this.isDirty =
          this.baseline !== null &&
          JSON.stringify(this.form.values) !== this.baseline;
      },
    },
  },
  methods: {
    populateForm() {
      this.form.setFieldValue(
        "map_pool",
        this.pool.maps.map((map) => map.id),
      );
      this.takeSnapshot();
    },
    takeSnapshot() {
      this.$nextTick(() => {
        this.baseline = JSON.stringify(this.form.values);
        this.isDirty = false;
      });
    },
    discardChanges() {
      this.populateForm();
    },
    updateMapPool(mapId: string) {
      const pool = Object.assign([], this.form.values.map_pool);
      if (pool.includes(mapId)) {
        pool.splice(pool.indexOf(mapId), 1);
      } else {
        pool.push(mapId);
      }

      this.form.setFieldValue("map_pool", pool);
    },
    async saveMapPool() {
      if (this.submitLock) {
        return;
      }
      this.submitLock = true;
      try {
        this.submitting = true;
        await this.$apollo.mutate({
          variables: {
            map_pool: this.form.values.map_pool,
          },
          mutation: generateMutation({
            delete__map_pool: [
              {
                where: {
                  map_pool_id: {
                    _eq: this.pool.id,
                  },
                  map_id: {
                    _nin: this.form.values.map_pool,
                  },
                },
              },
              {
                affected_rows: true,
              },
            ],
          }),
        });

        await this.$apollo.mutate({
          variables: {
            map_pool: this.form.values.map_pool,
          },
          mutation: generateMutation({
            insert__map_pool: [
              {
                objects: this.form.values.map_pool.map((mapId) => ({
                  map_pool_id: this.pool.id,
                  map_id: mapId,
                })),
                on_conflict: {
                  constraint: "map_pool_pkey",
                  update_columns: [],
                },
              },
              {
                affected_rows: true,
              },
            ],
          }),
        });

        toast({
          title: this.$t("pages.map_pool.save_success"),
        });

        // Keep Trios ranked pool in step with Competitive settings (same
        // map names, separate map rows).
        if (this.pool.type === "Competitive") {
          await this.syncTriosPoolFromCompetitive();
        }

        this.takeSnapshot();
      } finally {
        this.submitLock = false;
        this.submitting = false;
      }
    },
    async syncTriosPoolFromCompetitive() {
      try {
        const selectedIds = this.form.values.map_pool || [];
        if (!selectedIds.length) return;

        const { data: compData } = await this.$apollo.query({
          query: generateQuery({
            maps: [
              { where: { id: { _in: selectedIds } } },
              {
                id: true,
                name: true,
                label: true,
                poster: true,
                patch: true,
                workshop_map_id: true,
                active_pool: true,
              },
            ],
          }),
          fetchPolicy: "network-only",
        });

        const compMaps = (compData?.maps || []) as Array<{
          id: string;
          name: string;
          label?: string | null;
          poster?: string | null;
          patch?: string | null;
          workshop_map_id?: string | null;
          active_pool?: boolean | null;
        }>;
        if (!compMaps.length) return;

        const compNames = compMaps.map((m) => m.name);

        const { data: triosData } = await this.$apollo.query({
          query: generateQuery({
            map_pools: [
              {
                where: {
                  type: { _eq: "Trios" },
                  enabled: { _eq: true },
                },
                limit: 1,
                order_by: [{ seed: order_by.desc }],
              },
              { id: true },
            ],
            maps: [
              {
                where: {
                  type: { _eq: "Trios" },
                  name: { _in: compNames },
                  deleted_at: { _is_null: true },
                },
              },
              { id: true, name: true, enabled: true },
            ],
          }),
          fetchPolicy: "network-only",
        });

        const triosPoolId = triosData?.map_pools?.[0]?.id as string | undefined;
        if (!triosPoolId) return;

        const existingByName = new Map<
          string,
          { id: string; enabled: boolean }
        >();
        for (const m of triosData?.maps || []) {
          existingByName.set(m.name, { id: m.id, enabled: !!m.enabled });
        }

        const missing = compMaps.filter((m) => !existingByName.has(m.name));
        if (missing.length) {
          const { data: inserted } = await this.$apollo.mutate({
            mutation: generateMutation({
              insert_maps: [
                {
                  objects: missing.map((m) => ({
                    name: m.name,
                    type: "Trios",
                    label: m.label,
                    poster: m.poster,
                    patch: m.patch,
                    workshop_map_id: m.workshop_map_id,
                    active_pool: m.active_pool ?? false,
                    enabled: true,
                  })),
                  on_conflict: {
                    constraint: "maps_name_type_key",
                    update_columns: [
                      "label",
                      "poster",
                      "patch",
                      "workshop_map_id",
                      "active_pool",
                      "enabled",
                    ],
                  },
                },
                { returning: { id: true, name: true } },
              ],
            }),
          });
          for (const m of inserted?.insert_maps?.returning || []) {
            existingByName.set(m.name, { id: m.id, enabled: true });
          }
        }

        // Re-enable any soft-disabled Trios twins that Competitive still uses.
        const disabledIds = [...existingByName.values()]
          .filter((m) => !m.enabled)
          .map((m) => m.id);
        if (disabledIds.length) {
          await this.$apollo.mutate({
            mutation: generateMutation({
              update_maps: [
                {
                  where: { id: { _in: disabledIds } },
                  _set: { enabled: true, deleted_at: null },
                },
                { affected_rows: true },
              ],
            }),
          });
        }

        const triosIds = compNames
          .map((name) => existingByName.get(name)?.id)
          .filter(Boolean) as string[];

        await this.$apollo.mutate({
          mutation: generateMutation({
            delete__map_pool: [
              {
                where: {
                  map_pool_id: { _eq: triosPoolId },
                  ...(triosIds.length
                    ? { map_id: { _nin: triosIds } }
                    : {}),
                },
              },
              { affected_rows: true },
            ],
          }),
        });

        if (triosIds.length) {
          await this.$apollo.mutate({
            mutation: generateMutation({
              insert__map_pool: [
                {
                  objects: triosIds.map((mapId: string) => ({
                    map_pool_id: triosPoolId,
                    map_id: mapId,
                  })),
                  on_conflict: {
                    constraint: "map_pool_pkey",
                    update_columns: [],
                  },
                },
                { affected_rows: true },
              ],
            }),
          });
        }
      } catch (error) {
        console.error("Failed to sync Trios map pool from Competitive", error);
      }
    },
  },
  computed: {
    sortedMaps() {
      return {
        workshop: this.availableMaps.filter((map) => map.workshop_map_id),
        official: this.availableMaps.filter((map) => !map.workshop_map_id),
      };
    },
  },
};
</script>
