#!/usr/bin/env bash
# Apply store VIP columns on the panel VPS (idempotent).
set -euo pipefail
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

SQL_FILE=/tmp/store_vip_fulfillment.sql
cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.store_products
  ADD COLUMN IF NOT EXISTS vip_server_id uuid
    REFERENCES public.servers (id) ON DELETE SET NULL,
  ADD COLUMN IF NOT EXISTS vip_duration text;
ALTER TABLE public.store_orders
  ADD COLUMN IF NOT EXISTS vip_granted_at timestamptz;
CREATE INDEX IF NOT EXISTS store_products_vip_server_idx
  ON public.store_products (vip_server_id)
  WHERE vip_server_id IS NOT NULL;
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/store_vip_fulfillment.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/store_vip_fulfillment.sql'
echo "Done. Restart hasura/api/web after images are updated, then Edit each VIP product and pick its dedicated server + 30d."
