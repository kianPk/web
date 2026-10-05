#!/usr/bin/env bash
# Apply hosted VIP shop columns, then restart api (image set separately).
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.hosted_servers
  ADD COLUMN IF NOT EXISTS vip_sale_enabled boolean NOT NULL DEFAULT false,
  ADD COLUMN IF NOT EXISTS vip_price_7d integer NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS vip_price_30d integer NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS vip_price_90d integer NOT NULL DEFAULT 0;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'hosted_servers_vip_price_7d_check'
  ) THEN
    ALTER TABLE public.hosted_servers
      ADD CONSTRAINT hosted_servers_vip_price_7d_check CHECK (vip_price_7d >= 0);
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'hosted_servers_vip_price_30d_check'
  ) THEN
    ALTER TABLE public.hosted_servers
      ADD CONSTRAINT hosted_servers_vip_price_30d_check CHECK (vip_price_30d >= 0);
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'hosted_servers_vip_price_90d_check'
  ) THEN
    ALTER TABLE public.hosted_servers
      ADD CONSTRAINT hosted_servers_vip_price_90d_check CHECK (vip_price_90d >= 0);
  END IF;
END $$;
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/hosted_vip_shop.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/hosted_vip_shop.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/hosted_vip_shop.sql'

echo "hosted VIP shop columns applied"
