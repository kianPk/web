#!/usr/bin/env bash
# Free disk on the CS2 game node without SSH (kubectl hostPath pod).
# Safe defaults: temp Steam downloads, old demos, unused container images,
# inventory extract scratch — never deletes the live CS2 install (game/).
#
# Run on control-plane:
#   GAME_NODE=b039e0b7-8505-4fe8-a811-425a00d8503a \
#   bash /tmp/cleanup-gamenode-disk-vps.sh           # report only
#   bash /tmp/cleanup-gamenode-disk-vps.sh --apply   # actually delete
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack
GAME_NODE="${GAME_NODE:-b039e0b7-8505-4fe8-a811-425a00d8503a}"
APPLY=0
for a in "$@"; do
  case "$a" in
    --apply|-a) APPLY=1 ;;
  esac
done

kubectl -n "$NS" delete pod inv-disk-tools --ignore-not-found --wait=true --timeout=60s >/dev/null 2>&1 || true

cat <<EOF | kubectl -n "$NS" apply -f -
apiVersion: v1
kind: Pod
metadata:
  name: inv-disk-tools
spec:
  nodeName: ${GAME_NODE}
  restartPolicy: Never
  hostPID: true
  containers:
    - name: tools
      image: nicolaka/netshoot:latest
      command: ["sleep", "1800"]
      securityContext:
        privileged: true
      volumeMounts:
        - name: opt5
          mountPath: /host-opt5stack
        - name: root
          mountPath: /host
  volumes:
    - name: opt5
      hostPath:
        path: /opt/5stack
        type: Directory
    - name: root
      hostPath:
        path: /
        type: Directory
EOF

kubectl -n "$NS" wait --for=condition=Ready pod/inv-disk-tools --timeout=180s

MODE=report
[[ "$APPLY" -eq 1 ]] && MODE=apply

kubectl -n "$NS" exec inv-disk-tools -- bash -lc "
set -euo pipefail
MODE='$MODE'
echo '=== df ==='
df -h /host /host-opt5stack

echo
echo '=== top consumers under /opt/5stack ==='
du -xh --max-depth=2 /host-opt5stack 2>/dev/null | sort -h | tail -40

echo
echo '=== steamapps temps ==='
du -sh /host-opt5stack/serverfiles/steamapps/downloading \
       /host-opt5stack/serverfiles/steamapps/temp \
       /host-opt5stack/serverfiles/steamapps/workshop \
       2>/dev/null || true

echo
echo '=== demos / recordings (if any) ==='
du -sh /host-opt5stack/demos /host-opt5stack/*/demos \
       /host-opt5stack/serverfiles/csgo/replays \
       /host/var/lib/rancher/k3s/agent/containerd \
       2>/dev/null || true

echo
echo '=== inventory extract work ==='
du -sh /host-opt5stack/models/cs2-model-extract \
       /host-opt5stack/models/cs2-model-extract/.work \
       /host-opt5stack/models/cs2-model-extract/composites \
       /host-opt5stack/models/cs2-model-extract/renders \
       /host-opt5stack/models/cs2-model-extract/images \
       /host-opt5stack/models/cs2-model-extract/paints \
       2>/dev/null || true

if [[ \"\$MODE\" != apply ]]; then
  echo
  echo 'Report only. Re-run with --apply to delete safe junk.'
  exit 0
fi

echo
echo '=== APPLY safe cleanup ==='
# 1) Stuck SteamCMD staging (safe if no update mid-flight)
for d in downloading temp; do
  p=/host-opt5stack/serverfiles/steamapps/\$d
  if [[ -d \"\$p\" ]]; then
    echo \"clear \$p (\$(du -sh \"\$p\" | awk '{print \$1}'))\"
    rm -rf \"\$p\"/* \"\$p\"/.[!.]* 2>/dev/null || true
  fi
done

# 2) Inventory extract scratch + stale panel leftovers we don't need for UI
#    Keep images/ and paints/ if present. composites/renders are optional 3D.
if [[ -d /host-opt5stack/models/cs2-model-extract/.work ]]; then
  echo \"clear extract .work (\$(du -sh /host-opt5stack/models/cs2-model-extract/.work | awk '{print \$1}'))\"
  rm -rf /host-opt5stack/models/cs2-model-extract/.work/*
fi
# Old 3D bake caches from panel era — UI needs images/, not these
for d in composites renders tests; do
  p=/host-opt5stack/models/cs2-model-extract/\$d
  if [[ -d \"\$p\" ]]; then
    echo \"clear \$d (\$(du -sh \"\$p\" | awk '{print \$1}'))\"
    rm -rf \"\$p\"
  fi
done

# 3) Uploaded/orphan demos older than 3 days (keep recent)
if [[ -d /host-opt5stack/demos ]]; then
  echo 'prune demos older than 3 days'
  find /host-opt5stack/demos -type f -mtime +3 -print -delete 2>/dev/null | tail -20 || true
fi

# 4) k3s/containerd unused images on THIS node
if command -v nsenter >/dev/null; then
  echo 'k3s crictl rmi --prune (unused images)'
  nsenter -t 1 -m -u -i -n -- k3s crictl rmi --prune 2>/dev/null \
    || nsenter -t 1 -m -u -i -n -- crictl rmi --prune 2>/dev/null \
    || echo '(crictl prune skipped)'
fi

# 5) journal vacuum
nsenter -t 1 -m -u -i -n -- journalctl --vacuum-size=200M 2>/dev/null || true

echo
echo '=== df after ==='
df -h /host /host-opt5stack
du -sh /host-opt5stack/serverfiles \
       /host-opt5stack/models/cs2-model-extract \
       2>/dev/null || true
"

kubectl -n "$NS" delete pod inv-disk-tools --ignore-not-found --wait=false >/dev/null 2>&1 || true
echo "Done (mode=$MODE)."
