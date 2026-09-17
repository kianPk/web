#!/usr/bin/env bash
# Run on the panel VPS after CI is green for kianpk/web and kianpk/api.
# Export Bale secrets in your shell first (do not commit them):
#   export BALE_BOT_TOKEN='...'
#   export BALE_PROVIDER_TOKEN='...'
#   export BALE_BOT_USERNAME='yguardbot'

set -euo pipefail
: "${BALE_BOT_TOKEN:?set BALE_BOT_TOKEN}"
: "${BALE_PROVIDER_TOKEN:?set BALE_PROVIDER_TOKEN}"
: "${BALE_BOT_USERNAME:=yguardbot}"

export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

SQL_FILE=/tmp/store_products_orders.sql
cat >"$SQL_FILE" <<'SQL'
CREATE TABLE IF NOT EXISTS public.store_products (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  title text NOT NULL,
  slug text NOT NULL,
  description text NOT NULL DEFAULT '',
  price_irr integer NOT NULL CHECK (price_irr >= 0),
  image_url text,
  active boolean NOT NULL DEFAULT true,
  sort_order integer NOT NULL DEFAULT 0,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT store_products_slug_key UNIQUE (slug)
);
CREATE INDEX IF NOT EXISTS store_products_active_sort_idx
  ON public.store_products (active, sort_order ASC, created_at DESC);
CREATE TABLE IF NOT EXISTS public.store_orders (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  product_id uuid NOT NULL REFERENCES public.store_products (id) ON DELETE RESTRICT,
  buyer_steam_id bigint NOT NULL REFERENCES public.players (steam_id) ON UPDATE CASCADE ON DELETE CASCADE,
  amount_irr integer NOT NULL CHECK (amount_irr >= 0),
  status text NOT NULL DEFAULT 'pending'
    CHECK (status IN ('pending', 'paid', 'failed', 'cancelled')),
  bale_payload text NOT NULL,
  bale_payment_charge_id text,
  created_at timestamptz NOT NULL DEFAULT now(),
  paid_at timestamptz,
  CONSTRAINT store_orders_bale_payload_key UNIQUE (bale_payload)
);
CREATE INDEX IF NOT EXISTS store_orders_buyer_idx
  ON public.store_orders (buyer_steam_id, created_at DESC);
CREATE INDEX IF NOT EXISTS store_orders_status_idx
  ON public.store_orders (status);
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/store_products_orders.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/store_products_orders.sql'

kubectl -n 5stack set env deploy/api \
  "BALE_BOT_TOKEN=${BALE_BOT_TOKEN}" \
  "BALE_PROVIDER_TOKEN=${BALE_PROVIDER_TOKEN}" \
  "BALE_BOT_USERNAME=${BALE_BOT_USERNAME}"

kubectl -n 5stack set image deploy/web web=ghcr.io/kianpk/web:latest
kubectl -n 5stack set image deploy/api api=ghcr.io/kianpk/api:latest
kubectl -n 5stack rollout restart deploy/web deploy/api
kubectl -n 5stack rollout status deploy/web --timeout=5m
kubectl -n 5stack rollout status deploy/api --timeout=5m

# Reload Hasura metadata so GraphQL sees the new tables (from updated api/hasura on disk if present)
if [ -d /root/5stack-panel ]; then
  echo "If your panel tracks hasura metadata from the api repo, apply metadata now."
fi

kubectl -n 5stack get pods | grep -E 'web|api|hasura' || true
curl -sS https://api.yguard.ir/store/status || true
echo
echo "Done. Smoke: Settings → Store → add product → /store → Buy → Bale."
