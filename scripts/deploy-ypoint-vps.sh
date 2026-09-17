#!/usr/bin/env bash
# Run on the panel VPS AFTER GitHub Actions built:
#   ghcr.io/kianpk/api:latest
#   ghcr.io/kianpk/web:latest
#
# Applies Ypoint wallet migration + rolls web/api/hasura to latest images.

set -euo pipefail
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

SQL_FILE=/tmp/ypoint_wallet.sql
cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.players
  ADD COLUMN IF NOT EXISTS ypoint_balance integer NOT NULL DEFAULT 0
  CHECK (ypoint_balance >= 0);

CREATE TABLE IF NOT EXISTS public.ypoint_ledger (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  steam_id bigint NOT NULL REFERENCES public.players (steam_id) ON UPDATE CASCADE ON DELETE CASCADE,
  delta integer NOT NULL,
  balance_after integer NOT NULL,
  reason text NOT NULL,
  ref_type text,
  ref_id text,
  created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ypoint_ledger_steam_idx
  ON public.ypoint_ledger (steam_id, created_at DESC);

ALTER TABLE public.store_products
  ADD COLUMN IF NOT EXISTS ypoint_amount integer
  CHECK (ypoint_amount IS NULL OR ypoint_amount > 0);

INSERT INTO public.settings (name, value) VALUES
  ('public.ypoint_cost_duel', '5'),
  ('public.ypoint_cost_wingman', '8'),
  ('public.ypoint_cost_draft_create', '10'),
  ('public.ypoint_cost_draft_join', '10')
ON CONFLICT (name) DO NOTHING;

INSERT INTO public.store_products
  (title, slug, description, price_irr, ypoint_amount, sort_order, active)
VALUES
  ('50 Ypoints', 'ypoint-50', 'Credit 50 Ypoints to your wallet.', 50000, 50, 10, true),
  ('100 Ypoints', 'ypoint-100', 'Credit 100 Ypoints to your wallet.', 90000, 100, 20, true),
  ('250 Ypoints', 'ypoint-250', 'Credit 250 Ypoints to your wallet.', 200000, 250, 30, true),
  ('500 Ypoints', 'ypoint-500', 'Credit 500 Ypoints to your wallet.', 350000, 500, 40, true)
ON CONFLICT (slug) DO NOTHING;
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/ypoint_wallet.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/ypoint_wallet.sql'

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
echo "Smoke checks:"
curl -sS "https://api.yguard.ir/ypoint/costs" || true
echo
curl -sS "https://yguard.ir/api/store/status" || true
echo
echo "Done. Check: TopNav YP badge, /store packs, Duel/Wingman cost, draft Deploy cost."
