#!/usr/bin/env bash
# Stop VIP purchases from provisioning hosted servers.
#
# Root causes we hit in prod:
# 1) k3s caches ghcr.io/kianpk/api:latest (IfNotPresent) → rollout restart keeps OLD code
# 2) lifecycle treated any paid order linked to a plan product as "buy server"
#
# This script seals VIP rows in Postgres (works even on old API) AND force-pulls a
# fresh API image.
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

# Full git SHA from api repo after CI finishes, e.g.
#   bash scripts/fix-vip-shop-no-server-vps.sh 8b454f1abcd...
# If omitted, force-pull :latest with Always + unique annotation.
API_SHA="${1:-}"

echo "=== 1) Postgres seal (required) ==="
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
  'Hosted VIP (internal)', 'hosted-vip-shop',
  'Internal bill carrier for hosted server VIP sales. Not sold in the store.',
  0, NULL, NULL, NULL, NULL, NULL, 9999, false
)
ON CONFLICT (slug) DO UPDATE SET
  price_irr = 0, ypoint_amount = NULL, vip_server_id = NULL,
  vip_duration = NULL, hosted_slots = NULL, subscription_tier = NULL,
  active = false;

CREATE OR REPLACE FUNCTION public.tbiu_store_orders_seal_vip_shop()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.hosted_kind = 'vip_shop'
     OR (NEW.product_title IS NOT NULL AND NEW.product_title ~* '^VIP[[:space:]]+(7d|30d|90d)')
  THEN
    NEW.hosted_fulfilled_at := COALESCE(NEW.hosted_fulfilled_at, now());
    IF NEW.hosted_kind IS NULL OR NEW.hosted_kind NOT IN ('new', 'renew', 'slots') THEN
      NEW.hosted_kind := 'vip_shop';
    END IF;
  END IF;
  RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tbiu_store_orders_seal_vip_shop ON public.store_orders;
CREATE TRIGGER tbiu_store_orders_seal_vip_shop
  BEFORE INSERT OR UPDATE ON public.store_orders
  FOR EACH ROW EXECUTE FUNCTION public.tbiu_store_orders_seal_vip_shop();

UPDATE public.store_orders
SET hosted_fulfilled_at = COALESCE(hosted_fulfilled_at, now()),
    hosted_kind = 'vip_shop'
WHERE hosted_fulfilled_at IS NULL
  AND status IN ('pending', 'paid')
  AND (
    hosted_kind = 'vip_shop'
    OR COALESCE(product_title, '') ~* '^VIP[[:space:]]+(7d|30d|90d)'
  );

SELECT 'vip_shop_total' AS k, count(*)::text AS v FROM store_orders WHERE hosted_kind = 'vip_shop'
UNION ALL
SELECT 'vip_shop_sealed', count(*)::text FROM store_orders
  WHERE hosted_kind = 'vip_shop' AND hosted_fulfilled_at IS NOT NULL
UNION ALL
SELECT 'trigger_present', CASE WHEN EXISTS (
  SELECT 1 FROM pg_trigger WHERE tgname = 'tbiu_store_orders_seal_vip_shop'
) THEN 'yes' ELSE 'NO' END;
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "postgres: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/seal_vip_hard.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/seal_vip_hard.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/seal_vip_hard.sql'

echo "=== 2) Force-pull API image ==="
if [[ -n "$API_SHA" ]]; then
  API_IMAGE="ghcr.io/kianpk/api:${API_SHA}"
  echo "Using SHA image: $API_IMAGE"
  kubectl -n 5stack set image deploy/api "api=${API_IMAGE}"
else
  echo "No SHA given — force-pulling :latest (Always + bump annotation)"
  kubectl -n 5stack set image deploy/api api=ghcr.io/kianpk/api:latest
fi

kubectl -n 5stack patch deploy api --type='json' -p='[
  {"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"Always"}
]' || true

# Change something unique so the rollout cannot be a no-op on cached :latest.
kubectl -n 5stack annotate deploy/api "yguard.io/force-pull=$(date +%s)" --overwrite

kubectl -n 5stack rollout restart deploy/api
# Delete pods so nodes must re-pull even with a sticky local image cache.
kubectl -n 5stack delete pods -l app=api --wait=false || true
kubectl -n 5stack rollout status deploy/api --timeout=6m

echo "=== 3) Verify ==="
kubectl -n 5stack get deploy api -o jsonpath='image={.spec.template.spec.containers[0].image} pull={.spec.template.spec.containers[0].imagePullPolicy}{"\n"}'
kubectl -n 5stack get pods -l app=api -o wide
echo "DONE — buy VIP once more. It must not create /hosting servers."
