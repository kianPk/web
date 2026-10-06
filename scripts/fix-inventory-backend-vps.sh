#!/usr/bin/env bash
# inventory.yguard.ir frontend OK; /api/* 503 → inventory-backend not Ready.
# Run on the VPS.
set -euo pipefail
NS=5stack

echo "== inventory pods =="
kubectl -n "$NS" get pods -o wide | grep -E 'NAME|inventory' || true

echo
echo "== events (backend) =="
kubectl -n "$NS" describe deploy/inventory-backend 2>/dev/null | tail -n 40 || true
kubectl -n "$NS" get events --field-selector involvedObject.name=inventory-backend --sort-by=.lastTimestamp 2>/dev/null | tail -n 20 || true

echo
echo "== secrets / node label =="
kubectl -n "$NS" get secret inventory-secrets -o name 2>/dev/null || echo "MISSING secret/inventory-secrets"
kubectl get nodes -L 5stack-inventory 2>/dev/null || true

echo
echo "== backend logs =="
kubectl -n "$NS" logs deploy/inventory-backend --tail=150 --all-containers=true 2>/dev/null || \
  kubectl -n "$NS" logs -l app=inventory-backend --tail=150 --all-containers=true 2>/dev/null || true

echo
echo "== restart =="
kubectl -n "$NS" rollout restart deploy/inventory-backend
kubectl -n "$NS" rollout status deploy/inventory-backend --timeout=5m

echo
echo "== probe =="
curl -sS -o /dev/null -w "catalog HTTP %{http_code}\n" https://inventory.yguard.ir/api/catalog || true
