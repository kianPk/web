#!/usr/bin/env bash
# Apply IRR (Toman) wallet columns on the panel VPS (idempotent).
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.players
  ADD COLUMN IF NOT EXISTS irr_balance bigint NOT NULL DEFAULT 0
  CHECK (irr_balance >= 0);

CREATE TABLE IF NOT EXISTS public.irr_ledger (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  steam_id bigint NOT NULL REFERENCES public.players (steam_id) ON UPDATE CASCADE ON DELETE CASCADE,
  delta bigint NOT NULL,
  balance_after bigint NOT NULL,
  reason text NOT NULL,
  ref_type text,
  ref_id text,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS irr_ledger_steam_idx
  ON public.irr_ledger (steam_id, created_at DESC);

CREATE UNIQUE INDEX IF NOT EXISTS irr_ledger_credit_idempotent_idx
  ON public.irr_ledger (steam_id, ref_type, ref_id)
  WHERE ref_type IS NOT NULL AND ref_id IS NOT NULL AND delta > 0;

ALTER TABLE public.store_orders
  DROP CONSTRAINT IF EXISTS store_orders_hosted_kind_check;

ALTER TABLE public.store_orders
  ADD CONSTRAINT store_orders_hosted_kind_check
    CHECK (hosted_kind IS NULL OR hosted_kind IN ('new', 'renew', 'slots', 'vip_shop'));

INSERT INTO public.store_products
  (title, slug, description, price_irr, ypoint_amount, vip_server_id,
   vip_duration, hosted_slots, subscription_tier, sort_order, active)
VALUES (
  'Hosted VIP (internal)',
  'hosted-vip-shop',
  'Internal bill carrier for hosted server VIP sales. Not sold in the store.',
  0, NULL, NULL, NULL, NULL, NULL, 9999, false
)
ON CONFLICT (slug) DO NOTHING;
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/irr_wallet.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/irr_wallet.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/irr_wallet.sql'

echo "IRR wallet + vip_shop constraint + carrier product applied"

# Seal any already-paid VIP shop orders so the lifecycle job cannot
# provision a hosted server from them (leftover from the plan-carrier bug).
SEAL_FILE="$(mktemp)"
cat >"$SEAL_FILE" <<'SQL'
UPDATE public.store_orders
SET hosted_fulfilled_at = COALESCE(hosted_fulfilled_at, now())
WHERE hosted_kind = 'vip_shop'
  AND status = 'paid'
  AND hosted_fulfilled_at IS NULL;
SQL
kubectl -n 5stack cp "$SEAL_FILE" "$PGPOD:/tmp/seal_vip_shop.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/seal_vip_shop.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/seal_vip_shop.sql'
rm -f "$SEAL_FILE"
echo "sealed unpaid-fulfillment VIP shop orders"
