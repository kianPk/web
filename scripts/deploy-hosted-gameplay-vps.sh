#!/usr/bin/env bash
# Apply friendly_fire / bunny_hop columns on hosted_servers.
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.hosted_servers
  ADD COLUMN IF NOT EXISTS friendly_fire boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS bunny_hop boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS parachute boolean NOT NULL DEFAULT false;
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/hosted_gameplay.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -v ON_ERROR_STOP=1 -f /tmp/hosted_gameplay.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -v ON_ERROR_STOP=1 -f /tmp/hosted_gameplay.sql'
echo "Done. Redeploy api+web after this migration."
