#!/usr/bin/env bash
# Run on the panel VPS AFTER GitHub Actions built:
#   ghcr.io/kianpk/api:latest
#   ghcr.io/kianpk/web:latest
#
# Applies Trios → Rush migration + rolls web/api/hasura to latest images.

set -euo pipefail
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Prefer the Hasura migration from the api repo when present next to web.
API_MIG="${ROOT}/../api/hasura/migrations/default/1912000000000_trios_to_rush/up.sql"
if [[ ! -f "$API_MIG" ]]; then
  API_MIG="/tmp/trios_to_rush.sql"
  echo "WARNING: local api migration missing; expecting $API_MIG already on this host"
fi

SQL_FILE=/tmp/trios_to_rush.sql
cp "$API_MIG" "$SQL_FILE"

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/trios_to_rush.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/trios_to_rush.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/trios_to_rush.sql'

echo "Pulling latest images..."
kubectl -n 5stack set image deploy/web web=ghcr.io/kianpk/web:latest
kubectl -n 5stack set image deploy/api api=ghcr.io/kianpk/api:latest
kubectl -n 5stack set image deploy/hasura migrations=ghcr.io/kianpk/api:latest
kubectl -n 5stack rollout restart deploy/web deploy/api deploy/hasura
kubectl -n 5stack rollout status deploy/web --timeout=5m
kubectl -n 5stack rollout status deploy/api --timeout=5m
kubectl -n 5stack rollout status deploy/hasura --timeout=5m

kubectl -n 5stack get pods | grep -E 'web|api|hasura' || true
echo
echo "Smoke: matchmaking_rush setting"
kubectl -n 5stack exec "$PGPOD" -- bash -lc "psql -U postgres -d postgres -tAc \"SELECT value FROM settings WHERE name='public.matchmaking_rush'\"" \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc "psql \"\$POSTGRES_CONNECTION_STRING\" -tAc \"SELECT value FROM settings WHERE name='public.matchmaking_rush'\""
echo
echo "Smoke: Rush map"
kubectl -n 5stack exec "$PGPOD" -- bash -lc "psql -U postgres -d postgres -tAc \"SELECT name,type,active_pool FROM maps WHERE type='Rush'\"" \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc "psql \"\$POSTGRES_CONNECTION_STRING\" -tAc \"SELECT name,type,active_pool FROM maps WHERE type='Rush'\""
echo
echo "Done. Hard-refresh the site and check Play → Rush."
