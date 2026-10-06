#!/usr/bin/env bash
# inventory.yguard.ir frontend is up; /api/* is 503 → inventory-backend unhealthy.
# Run on the VPS (ubuntu + kubectl).
set -euo pipefail

NS=5stack

echo "== pods =="
kubectl -n "$NS" get pods -l 'app in (inventory-backend,inventory-frontend)' -o wide || \
  kubectl -n "$NS" get pods | grep -E 'inventory|NAME' || true

echo
echo "== deploy =="
kubectl -n "$NS" get deploy -l 'app in (inventory-backend,inventory-frontend)' || \
  kubectl -n "$NS" get deploy | grep -E 'inventory|NAME' || true

echo
echo "== endpoints =="
kubectl -n "$NS" get endpoints inventory-backend inventory-frontend 2>/dev/null || true

echo
echo "== recent backend logs =="
kubectl -n "$NS" logs deploy/inventory-backend --tail=120 --all-containers=true 2>/dev/null || \
  kubectl -n "$NS" logs -l app=inventory-backend --tail=120 --all-containers=true 2>/dev/null || true

echo
echo "== restart backend =="
kubectl -n "$NS" rollout restart deploy/inventory-backend
kubectl -n "$NS" rollout status deploy/inventory-backend --timeout=5m

echo
echo "== probe =="
curl -sS -o /dev/null -w "catalog HTTP %{http_code}\n" https://inventory.yguard.ir/api/catalog || true
