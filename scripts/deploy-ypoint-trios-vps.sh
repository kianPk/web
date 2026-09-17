#!/usr/bin/env bash
# Run on the panel VPS AFTER GitHub Actions built:
#   ghcr.io/kianpk/api:latest
#   ghcr.io/kianpk/web:latest
#
# Sets Trios = 12 Ypoints + ranked_free toggle, then rolls web/api.

set -euo pipefail
export KUBECONFIG=/etc/rancher/k3s/k3s.yaml

SQL_FILE=/tmp/ypoint_trios.sql
cat >"$SQL_FILE" <<'SQL'
INSERT INTO public.settings (name, value) VALUES
  ('public.ypoint_cost_trios', '12'),
  ('public.ypoint_ranked_free', 'false')
ON CONFLICT (name) DO NOTHING;

UPDATE public.settings SET value = '12' WHERE name = 'public.ypoint_cost_trios';
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep timescaledb | head -1 | cut -d/ -f2)
echo "Using postgres pod: $PGPOD"
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/ypoint_trios.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/ypoint_trios.sql'

echo "Rolling web + api..."
kubectl -n 5stack set image deploy/web web=ghcr.io/kianpk/web:latest
kubectl -n 5stack set image deploy/api api=ghcr.io/kianpk/api:latest
kubectl -n 5stack rollout restart deploy/web deploy/api
kubectl -n 5stack rollout status deploy/web --timeout=5m
kubectl -n 5stack rollout status deploy/api --timeout=5m

echo
curl -sS "https://api.yguard.ir/ypoint/costs" || true
echo
echo "Done. Admin: Settings → Application → Ypoints"
