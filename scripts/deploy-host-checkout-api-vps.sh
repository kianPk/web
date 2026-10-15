#!/usr/bin/env bash
# Force-pull API so POST /hosted-servers/host-checkout exists.
# Without this, Host pay shows: Cannot POST /hosted-servers/section-host/checkout
#
# Usage (after GitHub Actions finishes for api main):
#   bash scripts/deploy-host-checkout-api-vps.sh
#   bash scripts/deploy-host-checkout-api-vps.sh <full-git-sha>
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack
API_SHA="${1:-}"

if [[ -n "$API_SHA" ]]; then
  IMAGE="ghcr.io/kianpk/api:${API_SHA}"
else
  IMAGE="ghcr.io/kianpk/api:latest"
fi

echo "=== Force-pull API for Host checkout: $IMAGE ==="
kubectl -n "$NS" set image deploy/api "api=${IMAGE}"
kubectl -n "$NS" patch deploy/api --type=json -p='[
  {"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"Always"}
]' || true
kubectl -n "$NS" annotate deploy/api "yguard.io/force-pull=$(date +%s)" --overwrite
kubectl -n "$NS" rollout restart deploy/api
kubectl -n "$NS" delete pods -l app=api --wait=false || true
kubectl -n "$NS" rollout status deploy/api --timeout=6m

echo "=== Verify image ==="
kubectl -n "$NS" get deploy/api -o jsonpath='image={.spec.template.spec.containers[0].image} pull={.spec.template.spec.containers[0].imagePullPolicy}{"\n"}'
kubectl -n "$NS" get pods -l app=api -o wide

echo "DONE — Host pay should hit POST /hosted-servers/host-checkout"
