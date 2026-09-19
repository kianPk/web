#!/usr/bin/env bash
# Adds multi-item cart support for store checkout (Bale Pay).
# Run once on the panel VPS, then redeploy api + web.
set -euo pipefail
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

SQL_FILE=/tmp/store_cart_items.sql
cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.store_orders
  ADD COLUMN IF NOT EXISTS cart_items jsonb,
  ADD COLUMN IF NOT EXISTS terms_accepted_at timestamptz;

COMMENT ON COLUMN public.store_orders.cart_items IS
  'Optional snapshot of cart lines [{product_id,title,price_irr,ypoint_amount,vip_*,subscription_tier}]';
COMMENT ON COLUMN public.store_orders.terms_accepted_at IS
  'When the buyer accepted store terms before opening Bale Pay';
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/store_cart_items.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/store_cart_items.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/store_cart_items.sql'

# Track new columns in Hasura (idempotent-ish: ignore errors if already tracked)
HASURA=$(kubectl -n 5stack get pods -o name | grep hasura | head -1 | cut -d/ -f2)
ADMIN=$(kubectl -n 5stack get secret hasura-secret -o jsonpath='{.data.HASURA_GRAPHQL_ADMIN_SECRET}' 2>/dev/null | base64 -d || true)
if [[ -n "$HASURA" && -n "$ADMIN" ]]; then
  kubectl -n 5stack exec "$HASURA" -- wget -qO- \
    --header="X-Hasura-Admin-Secret: $ADMIN" \
    --header="Content-Type: application/json" \
    --post-data='{"type":"pg_track_table","args":{"source":"default","table":{"schema":"public","name":"store_orders"}}}' \
    http://127.0.0.1:8080/v1/metadata >/dev/null 2>&1 || true
  echo "Hasura: ensure store_orders.cart_items + terms_accepted_at are tracked (reload metadata if needed)."
fi

echo "Done. Redeploy api+web after this migration."
