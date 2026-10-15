#!/usr/bin/env bash
# Diagnose + unblock paid Host (section) orders that never got a hosted_servers row.
# Run on the panel VPS:
#   bash scripts/fix-host-fulfill-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack

SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
-- Carrier product must have hosted_slots for older fulfill gates / catalog joins.
INSERT INTO public.store_products (
  slug, title, description, price_irr, price_ypoint, hosted_slots,
  vip_duration, hosted_section_mode, sort_order, active
) VALUES (
  'host-section-dynamic',
  'Host (custom)',
  'Internal carrier for Servers Host rentals priced by duration.',
  0, NULL, 20, '1d', 'any', 9998, false
)
ON CONFLICT (slug) DO UPDATE SET
  hosted_slots = 20,
  hosted_section_mode = 'any',
  active = false,
  updated_at = now();

SELECT 'carrier' AS k, id::text, hosted_slots::text, active::text
  FROM store_products WHERE slug = 'host-section-dynamic';

SELECT 'recent_host_orders' AS k,
       o.id::text,
       o.status,
       o.hosted_kind,
       COALESCE(o.hosted_section_mode, '-') AS section,
       COALESCE(o.hosted_duration, '-') AS duration,
       COALESCE(o.hosted_requested_slots::text, '-') AS slots,
       COALESCE(o.hosted_fulfilled_at::text, 'NULL') AS fulfilled_at,
       COALESCE(o.hosted_server_id::text, 'NULL') AS hosted_server_id,
       COALESCE(o.paid_at::text, '-') AS paid_at,
       left(COALESCE(o.product_title, p.title, ''), 48) AS title
  FROM store_orders o
  LEFT JOIN store_products p ON p.id = o.product_id
 WHERE o.created_at > now() - interval '3 days'
   AND (
     o.hosted_kind IN ('new', 'renew', 'slots')
     OR o.hosted_section_mode IS NOT NULL
     OR o.product_title ILIKE 'Host %'
     OR p.slug = 'host-section-dynamic'
   )
 ORDER BY o.created_at DESC
 LIMIT 20;

-- Unstick: paid, claimed fulfilled, but no server row linked.
UPDATE store_orders
   SET hosted_fulfilled_at = NULL
 WHERE status = 'paid'
   AND hosted_kind IN ('new', 'renew')
   AND hosted_server_id IS NULL
   AND hosted_fulfilled_at IS NOT NULL
   AND paid_at > now() - interval '14 days';

SELECT 'unfulfilled_paid' AS k, count(*)::text AS v
  FROM store_orders
 WHERE status = 'paid'
   AND hosted_fulfilled_at IS NULL
   AND hosted_kind IN ('new', 'renew', 'slots')
   AND hosted_server_id IS NULL
   AND paid_at > now() - interval '14 days';

SELECT 'hosted_servers_recent' AS k,
       id::text, status, COALESCE(section_mode, '-'),
       COALESCE(status_detail, '-'),
       owner_steam_id::text, created_at::text
  FROM hosted_servers
 WHERE created_at > now() - interval '3 days'
 ORDER BY created_at DESC
 LIMIT 15;
SQL

PGPOD=$(kubectl -n "$NS" get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "postgres: $PGPOD"
kubectl -n "$NS" cp "$SQL_FILE" "$PGPOD:/tmp/fix_host_fulfill.sql"
kubectl -n "$NS" exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/fix_host_fulfill.sql' \
  || kubectl -n "$NS" exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/fix_host_fulfill.sql'

echo ""
echo "=== API image / recent fulfill logs ==="
kubectl -n "$NS" get deploy/api -o jsonpath='image={.spec.template.spec.containers[0].image}{"\n"}'
kubectl -n "$NS" get pods -l app=api -o wide
kubectl -n "$NS" logs deploy/api --tail=200 2>/dev/null \
  | grep -E 'hosted fulfill|hosted fulfillment|ProcessHostedServers|Store order paid|fulfill skipped' \
  || echo "(no matching API log lines yet)"

echo ""
echo "DONE — after deploying the API fix, lifecycle runs every 5 minutes and will fulfill stuck paid Host orders."
echo "Or restart API now: kubectl -n 5stack rollout restart deploy/api"
