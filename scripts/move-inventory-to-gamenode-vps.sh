#!/usr/bin/env bash
# Panel disk is too small for CS2 (~12G free). Move inventory pods onto the
# game node that already has /opt/5stack/serverfiles — no SSH required
# (uses a short-lived hostPath inspect/copy pod via kubectl).
#
# Run on the control-plane (ubuntu):
#   GAME_NODE=b039e0b7-8505-4fe8-a811-425a00d8503a \
#   bash /tmp/move-inventory-to-gamenode-vps.sh
#
# Flags:
#   --extract   also run extract-models.sh after pods land (long)
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

PANEL_NODE="${PANEL_NODE:-ubuntu}"
GAME_NODE="${GAME_NODE:-b039e0b7-8505-4fe8-a811-425a00d8503a}"
NS=5stack
DO_EXTRACT=0
COPY_MODELS=0
for a in "$@"; do
  case "$a" in
    --extract|-e) DO_EXTRACT=1 ;;
    --copy-models) COPY_MODELS=1 ;;
  esac
done

cleanup() {
  kubectl -n "$NS" delete pod inv-node-tools --wait=false --ignore-not-found >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "=== preflight: hostPath inspect pod on game node (no SSH) ==="
# Must fully delete first — kubectl apply cannot mutate securityContext / volumes.
kubectl -n "$NS" delete pod inv-node-tools --ignore-not-found --wait=true --timeout=120s >/dev/null 2>&1 || true
# Extra settle in case finalizer lags
for _ in 1 2 3 4 5; do
  kubectl -n "$NS" get pod inv-node-tools >/dev/null 2>&1 || break
  sleep 2
done

cat <<EOF | kubectl -n "$NS" apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: inv-node-tools
  labels:
    app: inv-node-tools
spec:
  nodeName: ${GAME_NODE}
  restartPolicy: Never
  containers:
    - name: tools
      image: nicolaka/netshoot:latest
      command: ["sleep", "3600"]
      volumeMounts:
        - name: opt5
          mountPath: /host-opt5stack
  volumes:
    - name: opt5
      hostPath:
        path: /opt/5stack
        type: Directory
EOF

echo "waiting for inv-node-tools..."
kubectl -n "$NS" wait --for=condition=Ready pod/inv-node-tools --timeout=180s

kubectl -n "$NS" exec inv-node-tools -- bash -lc '
  set -euo pipefail
  df -h /host-opt5stack / 2>/dev/null || df -h /
  echo
  if [[ -f /host-opt5stack/serverfiles/game/csgo/pak01_dir.vpk ]]; then
    ls -lh /host-opt5stack/serverfiles/game/csgo/pak01_dir.vpk
    du -sh /host-opt5stack/serverfiles
  else
    echo "FATAL: game node missing serverfiles/game/csgo/pak01_dir.vpk"
    ls -la /host-opt5stack/serverfiles 2>/dev/null || ls -la /host-opt5stack || true
    find /host-opt5stack -maxdepth 3 -type d 2>/dev/null | head -40
    exit 1
  fi
  mkdir -p /host-opt5stack/models/cs2-model-extract
'

echo
if [[ "$COPY_MODELS" -eq 1 ]]; then
  echo "=== copy models extract panel → game node (optional, slow) ==="
  if [[ -d /opt/5stack/models/cs2-model-extract ]]; then
    tar -C /opt/5stack/models/cs2-model-extract -cf - . \
      | kubectl -n "$NS" exec -i inv-node-tools -- bash -lc \
        'mkdir -p /host-opt5stack/models/cs2-model-extract && tar -C /host-opt5stack/models/cs2-model-extract -xf -'
    echo "models tree copied."
  else
    echo "(no local models extract to copy)"
  fi
else
  echo "=== skip models copy (default) — extract will rebuild images/ on game node ==="
  echo "    (pass --copy-models only if you need old composites/renders)"
  kubectl -n "$NS" exec inv-node-tools -- bash -lc \
    'mkdir -p /host-opt5stack/models/cs2-model-extract'
fi

echo
echo "=== relabel nodes ==="
kubectl label node "$PANEL_NODE" 5stack-inventory- --overwrite || true
kubectl label node "$GAME_NODE" 5stack-inventory=true --overwrite
kubectl get nodes -L 5stack-inventory

echo
echo "=== bounce inventory pods onto game node ==="
kubectl -n "$NS" delete pod -l app=inventory-backend --wait=false || true
kubectl -n "$NS" delete pod -l app=inventory-frontend --wait=false || true
kubectl -n "$NS" rollout status deploy/inventory-backend --timeout=5m
kubectl -n "$NS" rollout status deploy/inventory-frontend --timeout=5m
kubectl -n "$NS" get pods -l 'app in (inventory-backend,inventory-frontend)' -o wide

BE=$(kubectl -n "$NS" get pods -l app=inventory-backend -o jsonpath='{.items[0].metadata.name}')
NODE_NOW=$(kubectl -n "$NS" get pod "$BE" -o jsonpath='{.spec.nodeName}')
echo "backend pod $BE on node $NODE_NOW"
if [[ "$NODE_NOW" != "$GAME_NODE" ]]; then
  echo "FATAL: backend did not schedule onto $GAME_NODE"
  exit 1
fi

echo
echo "=== verify mounts ==="
kubectl -n "$NS" exec "$BE" -- bash -lc '
  ls -lh /cs2-game/game/csgo/pak01_dir.vpk
  echo images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)
'

if [[ "$DO_EXTRACT" -eq 1 ]]; then
  echo
  echo "=== extract-models.sh (30–90+ min) ==="
  kubectl -n "$NS" exec "$BE" -- bash -lc '
    set -euo pipefail
    export CS2_DIR=/cs2-game
    export OUT_DIR=/cs2-models
    export WORK_DIR=${EXTRACT_WORK_DIR:-/cs2-models/.work}
    mkdir -p "$OUT_DIR" "$WORK_DIR"
    bash /app/scripts/extract-models.sh
  '
  curl -sS -o /dev/null -w "sample image %{http_code}\n" \
    https://inventory.yguard.ir/images/weapon_ak47_4797ec49.webp || true
else
  echo
  echo "Skipped extract. When ready:"
  echo "  bash $0 --extract"
  echo "Or:"
  echo "  kubectl -n $NS exec deploy/inventory-backend -- bash /app/scripts/extract-models.sh"
fi

echo "Done. Ctrl+F5 /apps/inventory after extract finishes."
