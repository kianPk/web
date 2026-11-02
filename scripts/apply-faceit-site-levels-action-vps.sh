#!/usr/bin/env bash
# Register syncFaceitSiteLevels on a live Hasura (if metadata was not reloaded).
# Prefer: set hasura migrations image to the new API SHA and rollout restart.
# Usage: bash scripts/apply-faceit-site-levels-action-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack

HASURA_POD=$(kubectl -n "$NS" get pods -o name | grep -E 'pod/hasura' | head -1 | cut -d/ -f2)
echo "hasura: $HASURA_POD"

ADMIN_SECRET=$(kubectl -n "$NS" get secret hasura-secrets -o jsonpath='{.data.HASURA_GRAPHQL_ADMIN_SECRET}' 2>/dev/null | base64 -d || true)
if [[ -z "${ADMIN_SECRET}" ]]; then
  ADMIN_SECRET=$(kubectl -n "$NS" get deploy hasura -o yaml \
    | awk '/HASURA_GRAPHQL_ADMIN_SECRET/{want=1; next} want && /value:/{gsub(/[" ]/,"",$2); print $2; exit}')
fi
[[ -n "${ADMIN_SECRET}" ]] || { echo "no admin secret" >&2; exit 1; }

HOOK=$(kubectl -n "$NS" get deploy hasura -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="HASURA_GRAPHQL_ACTIONS_HOOK")].value}')
HOOK="${HOOK:-http://api:5585/hasura/actions}"

meta() {
  kubectl -n "$NS" exec "$HASURA_POD" -c hasura -- bash -lc "
curl -sS -X POST http://127.0.0.1:8080/v1/metadata \
  -H 'Content-Type: application/json' \
  -H 'x-hasura-admin-secret: ${ADMIN_SECRET}' \
  -d $(printf '%q' "$1")
"
}

echo "==> ensure custom type FaceitSyncSiteLevelsOutput"
# Export + merge is heavy; create_action fails clearly if type missing.
# Best path: restart hasura with new migrations image. This script only creates the action.
echo "==> create_action syncFaceitSiteLevels (ignore already-exists)"
meta "{\"type\":\"create_action\",\"args\":{\"name\":\"syncFaceitSiteLevels\",\"definition\":{\"kind\":\"synchronous\",\"handler\":\"${HOOK}\",\"forward_client_headers\":true,\"type\":\"mutation\",\"arguments\":[],\"output_type\":\"FaceitSyncSiteLevelsOutput\",\"timeout\":120},\"comment\":\"Raise site XP from stored FACEIT skill\"}}" || true

echo "==> permission administrator"
meta "{\"type\":\"create_action_permission\",\"args\":{\"action\":\"syncFaceitSiteLevels\",\"role\":\"administrator\"}}" || true

echo "==> reload metadata"
meta '{"type":"reload_metadata","args":{"reload_remote_schemas":true}}'

echo "done — if create_action failed on missing type, run:"
echo "  kubectl -n 5stack set image deploy/hasura migrations=ghcr.io/kianpk/api:<API_SHA>"
echo "  kubectl -n 5stack rollout restart deploy/hasura"
