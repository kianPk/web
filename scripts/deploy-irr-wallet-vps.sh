#!/usr/bin/env bash
# Apply IRR (Toman) wallet columns on the panel VPS (idempotent)
# and backfill missing VIP-shop owner credits.
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

-- Backfill owner credits for already-paid VIP shop orders missing a ledger row.
WITH paid AS (
  SELECT o.id,
         CASE
           WHEN COALESCE(o.amount_irr, 0) > 0 THEN o.amount_irr
           ELSE COALESCE((
             SELECT SUM(COALESCE((x->>'price_irr')::bigint, 0))
             FROM jsonb_array_elements(COALESCE(o.cart_items, '[]'::jsonb)) AS x
           ), 0)
         END AS amount_irr,
         o.hosted_server_id
  FROM store_orders o
  WHERE o.status = 'paid'
    AND (
      o.hosted_kind = 'vip_shop'
      OR COALESCE(o.product_title, '') ~* '^VIP[[:space:]]+(7d|30d|90d)'
    )
),
owner_row AS (
  SELECT p.id AS order_id, p.amount_irr, h.owner_steam_id
  FROM paid p
  JOIN hosted_servers h ON h.id = p.hosted_server_id
  WHERE p.amount_irr > 0
    AND h.owner_steam_id IS NOT NULL
    AND NOT EXISTS (
      SELECT 1 FROM irr_ledger l
      WHERE l.steam_id = h.owner_steam_id
        AND l.ref_type = 'hosted_vip_earning'
        AND l.ref_id = p.id::text
        AND l.delta > 0
    )
),
credited AS (
  UPDATE players pl
  SET irr_balance = pl.irr_balance + o.amount_irr
  FROM owner_row o
  WHERE pl.steam_id = o.owner_steam_id
  RETURNING pl.steam_id, pl.irr_balance, o.order_id, o.amount_irr
)
INSERT INTO irr_ledger (steam_id, delta, balance_after, reason, ref_type, ref_id)
SELECT c.steam_id, c.amount_irr, c.irr_balance, 'hosted_vip_earning',
       'hosted_vip_earning', c.order_id::text
FROM credited c;

SELECT 'backfilled_vip_credits' AS k, count(*)::text AS v FROM irr_ledger
WHERE ref_type = 'hosted_vip_earning';
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "postgres: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/irr_wallet.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -v ON_ERROR_STOP=1 -f /tmp/irr_wallet.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -v ON_ERROR_STOP=1 -f /tmp/irr_wallet.sql'

echo "IRR wallet schema + VIP owner credit backfill done"
