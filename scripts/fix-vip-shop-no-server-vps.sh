#!/usr/bin/env bash
# CRITICAL: stop VIP shop Bale orders from provisioning hosted servers.
# Safe to re-run. Does not require a new API image (DB trigger is enough),
# but you should still roll api to latest afterward.
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
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
ON CONFLICT (slug) DO UPDATE SET
  price_irr = 0,
  ypoint_amount = NULL,
  vip_server_id = NULL,
  vip_duration = NULL,
  hosted_slots = NULL,
  subscription_tier = NULL,
  active = false;

CREATE OR REPLACE FUNCTION public.tbiu_store_orders_seal_vip_shop()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  IF NEW.hosted_kind = 'vip_shop' THEN
    NEW.hosted_fulfilled_at := COALESCE(NEW.hosted_fulfilled_at, now());
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tbiu_store_orders_seal_vip_shop ON public.store_orders;
CREATE TRIGGER tbiu_store_orders_seal_vip_shop
  BEFORE INSERT OR UPDATE ON public.store_orders
  FOR EACH ROW
  EXECUTE FUNCTION public.tbiu_store_orders_seal_vip_shop();

UPDATE public.store_orders
SET hosted_fulfilled_at = COALESCE(hosted_fulfilled_at, now())
WHERE hosted_kind = 'vip_shop'
  AND hosted_fulfilled_at IS NULL;

-- Also seal paid orders whose title looks like VIP shop but kind was lost /
-- never set, if they are still unfulfilled and linked to a plan product.
UPDATE public.store_orders o
SET hosted_fulfilled_at = COALESCE(o.hosted_fulfilled_at, now()),
    hosted_kind = COALESCE(o.hosted_kind, 'vip_shop')
WHERE o.status = 'paid'
  AND o.hosted_fulfilled_at IS NULL
  AND o.product_title ILIKE 'VIP %'
  AND o.payment_method = 'bale'
  AND o.paid_at > now() - interval '14 days';
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/seal_vip_shop_hard.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/seal_vip_shop_hard.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/seal_vip_shop_hard.sql'

echo "VIP shop seal trigger applied"

kubectl -n 5stack set image deploy/api api=ghcr.io/kianpk/api:latest
kubectl -n 5stack rollout restart deploy/api
kubectl -n 5stack rollout status deploy/api --timeout=5m
kubectl -n 5stack get deploy api -o jsonpath='{.spec.template.spec.containers[0].image}{"\n"}'
