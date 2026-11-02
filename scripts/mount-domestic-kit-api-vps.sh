#!/usr/bin/env bash
# Mount /opt/5stack/domestic-kit into the API pod and enable airgap pull policy.
# Run on the panel after prepare-domestic-node-kit-vps.sh.
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

KIT_ROOT="${KIT_ROOT:-/opt/5stack/domestic-kit}"
mkdir -p "$KIT_ROOT"

kubectl -n 5stack set env deploy/api \
  DOMESTIC_KIT_PATH=/opt/5stack/domestic-kit \
  AIRGAP_GAME_NODES=true

# Add volume + mount if not already present
HAS_VOL=$(kubectl -n 5stack get deploy api -o jsonpath='{.spec.template.spec.volumes[?(@.name=="domestic-kit")].name}' || true)
if [[ -z "${HAS_VOL}" ]]; then
  kubectl -n 5stack patch deploy/api --type=json -p="[
    {\"op\":\"add\",\"path\":\"/spec/template/spec/volumes/-\",\"value\":{\"name\":\"domestic-kit\",\"hostPath\":{\"path\":\"${KIT_ROOT}\",\"type\":\"DirectoryOrCreate\"}}},
    {\"op\":\"add\",\"path\":\"/spec/template/spec/containers/0/volumeMounts/-\",\"value\":{\"name\":\"domestic-kit\",\"mountPath\":\"/opt/5stack/domestic-kit\",\"readOnly\":true}}
  ]"
fi

kubectl -n 5stack rollout status deploy/api --timeout=5m
echo "API now serves domestic kit from ${KIT_ROOT}"
echo "Check: curl -sS \"\$API_DOMAIN/game-server-node/domestic/status\""
