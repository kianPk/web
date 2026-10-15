#!/usr/bin/env bash
# Fix inventory UI placeholders (blurry circles) + print InvSim steps for in-game skins.
#
# Root cause of blank skin art: catalog metadata is fine, but
#   https://inventory.yguard.ir/images/*.webp → 404
# because /opt/5stack/models/cs2-model-extract/images is empty / never extracted.
#
# Run on the panel / inventory node (has k3s + 5stack-inventory=true):
#   curl -fsSL -o /tmp/fix-inventory-assets-vps.sh \
#     https://raw.githubusercontent.com/kianPk/web/main/scripts/fix-inventory-assets-vps.sh
#   bash /tmp/fix-inventory-assets-vps.sh           # diagnose
#   bash /tmp/fix-inventory-assets-vps.sh --extract # diagnose + kick extract
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack
HOST_EXTRACT=/opt/5stack/models/cs2-model-extract
HOST_CS2=/opt/5stack/serverfiles
DO_EXTRACT=0
for a in "$@"; do
  case "$a" in
    --extract|-e) DO_EXTRACT=1 ;;
  esac
done

echo "=== inventory pods ==="
kubectl -n "$NS" get pods -o wide | grep -E 'NAME|inventory' || true

echo
echo "=== host CS2 install (required for extract) ==="
if [[ -d "$HOST_CS2" ]]; then
  echo "host: $HOST_CS2"
  ls -la "$HOST_CS2" | head -20
  if [[ -f "$HOST_CS2/game/csgo/pak01_dir.vpk" ]]; then
    echo "CS2 OK: game/csgo/pak01_dir.vpk present"
    ls -lh "$HOST_CS2/game/csgo/pak01_dir.vpk" || true
  else
    echo "CS2 BROKEN/INCOMPLETE: missing game/csgo/pak01_dir.vpk"
    echo "  inventory-backend mounts this at /cs2-game (read-only)."
    echo "  Fix: install CS2 dedicated files onto this node, e.g.:"
    echo "    mkdir -p /opt/5stack/serverfiles"
    echo "    # steamcmd + app 730 into /opt/5stack/serverfiles"
    echo "    # until game/csgo/pak01_dir.vpk exists"
    find "$HOST_CS2" -maxdepth 3 -type d 2>/dev/null | head -40 || true
  fi
else
  echo "MISSING $HOST_CS2"
fi

echo
echo "=== hostPath extract tree ==="
if [[ -d "$HOST_EXTRACT" ]]; then
  echo "host: $HOST_EXTRACT"
  du -sh "$HOST_EXTRACT"/* 2>/dev/null | head -20 || true
  echo "images count: $(find "$HOST_EXTRACT/images" -type f 2>/dev/null | wc -l)"
  echo "paints count: $(find "$HOST_EXTRACT/paints" -type f 2>/dev/null | wc -l)"
  ls "$HOST_EXTRACT/images" 2>/dev/null | head -5 || echo "(no images/)"
else
  echo "MISSING $HOST_EXTRACT — creating DirectoryOrCreate path"
  mkdir -p "$HOST_EXTRACT"
fi

echo
echo "=== mount inside inventory-frontend ==="
FE=$(kubectl -n "$NS" get pods -l app=inventory-frontend -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)
if [[ -n "${FE:-}" ]]; then
  kubectl -n "$NS" exec "$FE" -- bash -lc '
    ls -la /cs2-models 2>&1 | head -20
    echo "images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)"
    ls /cs2-models/images 2>/dev/null | head -5 || true
  ' || true
else
  echo "(no inventory-frontend pod)"
fi

echo
echo "=== mount inside inventory-backend ==="
BE=$(kubectl -n "$NS" get pods -l app=inventory-backend -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)
if [[ -n "${BE:-}" ]]; then
  kubectl -n "$NS" exec "$BE" -- bash -lc '
    ls -la /cs2-models /cs2-game 2>&1 | head -40
    echo "images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)"
    if [[ -f /cs2-game/game/csgo/pak01_dir.vpk ]]; then
      echo "cs2-game: OK (pak01_dir.vpk)"
    else
      echo "cs2-game: MISSING game/csgo/pak01_dir.vpk (host /opt/5stack/serverfiles incomplete)"
      ls -la /cs2-game 2>&1 | head -20
      find /cs2-game -maxdepth 3 -type d 2>/dev/null | head -30
    fi
    ls /app/scripts/extract-models.sh 2>/dev/null || \
      find /app /opt -name extract-models.sh 2>/dev/null | head -5 || true
  ' || true
else
  echo "(no inventory-backend pod)"
fi

echo
echo "=== public probes ==="
curl -sS -o /dev/null -w "catalog %{http_code}\n" https://inventory.yguard.ir/api/catalog || true
SAMPLE=$(curl -sS https://inventory.yguard.ir/api/catalog \
  | sed -n 's/.*"image":"\([^"]*\)".*/\1/p' | head -1)
if [[ -n "${SAMPLE:-}" ]]; then
  echo "sample image path: $SAMPLE"
  curl -sS -o /dev/null -w "sample image %{http_code}\n" "https://inventory.yguard.ir${SAMPLE}" || true
else
  echo "could not pick a sample image from catalog"
  SAMPLE=/images/weapon_ak47_4797ec49.webp
  curl -sS -o /dev/null -w "weapon_ak47 hardcoded %{http_code}\n" \
    "https://inventory.yguard.ir${SAMPLE}" || true
fi
curl -sS -o /dev/null -w "equipped/v5 %{http_code}\n" \
  https://inventory.yguard.ir/api/equipped/v5/76561198000000000 || true

if [[ "$DO_EXTRACT" -eq 1 ]]; then
  echo
  echo "=== EXTRACT (backend script via bash) ==="
  if [[ -z "${BE:-}" ]]; then
    echo "FATAL: no inventory-backend pod"
    exit 1
  fi

  if [[ ! -f "$HOST_CS2/game/csgo/pak01_dir.vpk" ]]; then
    echo "FATAL: cannot extract — CS2 files missing on this node."
    echo "Populate $HOST_CS2 so that game/csgo/pak01_dir.vpk exists, then re-run --extract."
    echo
    echo "Quick check on host:"
    echo "  ls -la $HOST_CS2"
    echo "  ls -la $HOST_CS2/game/csgo/pak01_dir.vpk"
    echo
    echo "If this node never had dedicated files, install SteamCMD app 730 into $HOST_CS2"
    echo "(same tree game-server pods use as /serverdata/serverfiles)."
    exit 1
  fi

  SCRIPT_PATH=$(kubectl -n "$NS" exec "$BE" -- bash -lc '
    for p in /app/scripts/extract-models.sh /scripts/extract-models.sh; do
      [ -f "$p" ] && echo "$p" && exit 0
    done
    find /app /opt -name extract-models.sh 2>/dev/null | head -1
  ' | tr -d '\r')

  if [[ -z "${SCRIPT_PATH:-}" ]]; then
    echo "FATAL: extract-models.sh not found in backend image."
    echo "Open https://yguard.ir/apps/inventory/admin as admin and click Extract models."
    exit 1
  fi

  echo "Running: CS2_DIR=/cs2-game OUT_DIR=/cs2-models bash $SCRIPT_PATH"
  echo "(this can take 30–90+ minutes — keep the session open)"
  # Must use bash: extract-models.sh needs pipefail; busybox/dash `sh` dies on set -o pipefail.
  kubectl -n "$NS" exec "$BE" -- bash -lc "
    set -euo pipefail
    export CS2_DIR=/cs2-game
    export OUT_DIR=/cs2-models
    export WORK_DIR=\${EXTRACT_WORK_DIR:-/cs2-models/.work}
    mkdir -p \"\$OUT_DIR\" \"\$WORK_DIR\"
    bash \"$SCRIPT_PATH\"
  "

  echo
  echo "=== post-extract counts ==="
  echo "host images: $(find "$HOST_EXTRACT/images" -type f 2>/dev/null | wc -l)"
  if [[ -n "${FE:-}" ]]; then
    kubectl -n "$NS" exec "$FE" -- bash -lc 'echo "pod images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)"' 2>/dev/null || true
  fi
  curl -sS -o /dev/null -w "sample image after extract %{http_code}\n" \
    "https://inventory.yguard.ir${SAMPLE}" || true
fi

echo
echo "=== NEXT STEPS ==="
echo "1) UI placeholders: need images/ under $HOST_EXTRACT (today count=0)."
echo "   Blocker if cs2-game incomplete: fix $HOST_CS2 first, then --extract."
echo "2) Skins in-game: YGuardInvSim 1.1+ + real key in"
echo "     /opt/5stack/custom-plugins/invsim_apikey"
echo "   (Inventory Admin → Server key), then restart dedicated servers."
echo "3) Hard-refresh /apps/inventory (Ctrl+F5) after extract finishes."
