#!/usr/bin/env bash
# Force-pull a fresh API image so Host admin rates actually persist.
# k3s often keeps a stale ghcr.io/kianpk/api:latest (IfNotPresent).
#
# Usage on the panel VPS (after GitHub Actions finishes for api main):
#   bash scripts/deploy-host-pricing-api-vps.sh
#   bash scripts/deploy-host-pricing-api-vps.sh <full-git-sha>
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack
API_SHA="${1:-}"

echo "=== Seed Host pricing settings keys (idempotent) ==="
SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT
cat >"$SQL_FILE" <<'SQL'
INSERT INTO public.settings ("name", "value") VALUES
  ('hosted_servers.section_host_price_irr_per_hour', '500000'),
  ('hosted_servers.section_host_price_ypoint_per_hour', '50'),
  ('hosted_servers.section_host_slot_price_irr_per_hour', '50000'),
  ('hosted_servers.section_host_slot_price_ypoint_per_hour', '5')
ON CONFLICT ("name") DO NOTHING;

SELECT name, value FROM public.settings
 WHERE name LIKE 'hosted_servers.section_host%'
 ORDER BY name;
SQL

PGPOD=$(kubectl -n "$NS" get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "postgres: $PGPOD"
kubectl -n "$NS" cp "$SQL_FILE" "$PGPOD:/tmp/host_pricing_seed.sql"
kubectl -n "$NS" exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/host_pricing_seed.sql' \
  || kubectl -n "$NS" exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/host_pricing_seed.sql'

echo "=== Force-pull API ==="
if [[ -n "$API_SHA" ]]; then
  IMAGE="ghcr.io/kianpk/api:${API_SHA}"
else
  IMAGE="ghcr.io/kianpk/api:latest"
fi
echo "image: $IMAGE"
kubectl -n "$NS" set image deploy/api "api=${IMAGE}"
kubectl -n "$NS" patch deploy/api --type=json -p='[
  {"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"Always"}
]' || true
kubectl -n "$NS" annotate deploy/api "yguard.io/force-pull=$(date +%s)" --overwrite
kubectl -n "$NS" rollout restart deploy/api
kubectl -n "$NS" delete pods -l app=api --wait=false || true
kubectl -n "$NS" rollout status deploy/api --timeout=6m

echo "=== Verify ==="
kubectl -n "$NS" get deploy/api -o jsonpath='image={.spec.template.spec.containers[0].image} pull={.spec.template.spec.containers[0].imagePullPolicy}{"\n"}'
kubectl -n "$NS" get pods -l app=api -o wide
echo "DONE — open Host as admin, Save rates, hard-refresh."
