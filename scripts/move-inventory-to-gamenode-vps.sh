#!/usr/bin/env bash
# Panel disk is too small for CS2 (~12G free / 50G total). Inventory hostPaths
# need a full /opt/5stack/serverfiles on the same node as the pods.
# This moves the 5stack-inventory=true label to the game node, copies the
# existing models extract mount, and restarts inventory pods there.
#
# Run on the control-plane (ubuntu):
#   GAME_NODE=b039e0b7-8505-4fe8-a811-425a00d8503a \
#   GAME_IP=100.87.196.94 \
#   bash scripts/move-inventory-to-gamenode-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

PANEL_NODE="${PANEL_NODE:-ubuntu}"
GAME_NODE="${GAME_NODE:-b039e0b7-8505-4fe8-a811-425a00d8503a}"
GAME_IP="${GAME_IP:-100.87.196.94}"
NS=5stack

echo "=== preflight on game node via SSH ($GAME_IP) ==="
ssh -o StrictHostKeyChecking=accept-new "root@${GAME_IP}" 'bash -s' <<'EOS'
set -euo pipefail
df -h / /opt/5stack 2>/dev/null || df -h /
echo
if [[ -f /opt/5stack/serverfiles/game/csgo/pak01_dir.vpk ]]; then
  ls -lh /opt/5stack/serverfiles/game/csgo/pak01_dir.vpk
  du -sh /opt/5stack/serverfiles
else
  echo "FATAL: game node missing /opt/5stack/serverfiles/game/csgo/pak01_dir.vpk"
  ls -la /opt/5stack/serverfiles 2>/dev/null || true
  exit 1
fi
avail_kb=$(df -Pk /opt/5stack 2>/dev/null | awk 'NR==2{print $4}')
avail_gb=$((avail_kb / 1024 / 1024))
echo "avail on game node: ${avail_gb}G"
if [[ "$avail_gb" -lt 20 ]]; then
  echo "WARN: less than 20G free — extract may need more for .work"
fi
mkdir -p /opt/5stack/models/cs2-model-extract
EOS

echo
echo "=== rsync models extract panel → game node (images currently empty; keep renders/composites) ==="
rsync -aH --info=progress2 \
  /opt/5stack/models/cs2-model-extract/ \
  "root@${GAME_IP}:/opt/5stack/models/cs2-model-extract/"

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

echo
echo "=== verify mounts on new backend pod ==="
BE=$(kubectl -n "$NS" get pods -l app=inventory-backend -o jsonpath='{.items[0].metadata.name}')
kubectl -n "$NS" exec "$BE" -- bash -lc '
  ls -lh /cs2-game/game/csgo/pak01_dir.vpk
  echo images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)
'

echo
echo "=== kick extract on game node (long) ==="
kubectl -n "$NS" exec "$BE" -- bash -lc '
  set -euo pipefail
  export CS2_DIR=/cs2-game
  export OUT_DIR=/cs2-models
  export WORK_DIR=${EXTRACT_WORK_DIR:-/cs2-models/.work}
  mkdir -p "$OUT_DIR" "$WORK_DIR"
  bash /app/scripts/extract-models.sh
'

echo
echo "=== probe ==="
curl -sS -o /dev/null -w "sample image %{http_code}\n" \
  https://inventory.yguard.ir/images/weapon_ak47_4797ec49.webp || true
echo "Done. Ctrl+F5 /apps/inventory"
