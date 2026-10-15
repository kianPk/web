#!/usr/bin/env bash
# Fix inventory UI placeholders (blurry circles) + print InvSim steps for in-game skins.
#
# Root cause of blank skin art: catalog metadata is fine, but
#   https://inventory.yguard.ir/images/*.webp → 404
# because /opt/5stack/models/cs2-model-extract/images is empty / never extracted.
#
# Run on the panel VPS:
#   cd /opt/5stack/web   # or wherever this repo is
#   bash scripts/fix-inventory-assets-vps.sh           # diagnose
#   bash scripts/fix-inventory-assets-vps.sh --extract # diagnose + kick extract Job
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack
DO_EXTRACT=0
for a in "$@"; do
  case "$a" in
    --extract|-e) DO_EXTRACT=1 ;;
  esac
done

echo "=== inventory pods ==="
kubectl -n "$NS" get pods -o wide | grep -E 'NAME|inventory' || true

echo
echo "=== hostPath extract tree ==="
HOST_EXTRACT=/opt/5stack/models/cs2-model-extract
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
  kubectl -n "$NS" exec "$FE" -- sh -lc '
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
  kubectl -n "$NS" exec "$BE" -- sh -lc '
    ls -la /cs2-models /cs2-game 2>&1 | head -40
    echo "images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)"
    test -d /cs2-game/game/csgo && echo "cs2-game: OK" || echo "cs2-game: MISSING (need /opt/5stack/serverfiles)"
    ls /app/scripts/extract-models.sh /scripts/extract-models.sh 2>/dev/null || \
      find /app /opt /scripts -name extract-models.sh 2>/dev/null | head -5 || true
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
  curl -sS -o /dev/null -w "weapon_ak47 hardcoded %{http_code}\n" \
    https://inventory.yguard.ir/images/weapon_ak47_4797ec49.webp || true
fi
curl -sS -o /dev/null -w "equipped/v5 %{http_code}\n" \
  https://inventory.yguard.ir/api/equipped/v5/76561198000000000 || true

if [[ "$DO_EXTRACT" -eq 1 ]]; then
  echo
  echo "=== EXTRACT (admin Job / backend script) ==="
  if [[ -z "${BE:-}" ]]; then
    echo "FATAL: no inventory-backend pod"
    exit 1
  fi

  # Prefer the plugin admin endpoint if a cookie/token is available; otherwise
  # run extract-models.sh inside the backend pod (same path the admin button uses).
  SCRIPT_PATH=$(kubectl -n "$NS" exec "$BE" -- sh -lc '
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

  echo "Running: CS2_DIR=/cs2-game OUT_DIR=/cs2-models $SCRIPT_PATH"
  echo "(this can take 30–90+ minutes — keep the session open)"
  kubectl -n "$NS" exec "$BE" -- sh -lc "
    set -euo pipefail
    export CS2_DIR=/cs2-game
    export OUT_DIR=/cs2-models
    export WORK_DIR=\${WORK_DIR:-/tmp/cs2-model-extract-work}
    mkdir -p \"\$OUT_DIR\" \"\$WORK_DIR\"
    bash \"$SCRIPT_PATH\"
  "

  echo
  echo "=== post-extract counts ==="
  echo "host images: $(find "$HOST_EXTRACT/images" -type f 2>/dev/null | wc -l)"
  kubectl -n "$NS" exec "$FE" -- sh -lc 'echo "pod images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)"' 2>/dev/null || true
  curl -sS -o /dev/null -w "sample image after extract %{http_code}\n" \
    "https://inventory.yguard.ir${SAMPLE:-/images/weapon_ak47_4797ec49.webp}" || true
fi

echo
echo "=== NEXT STEPS ==="
echo "1) UI placeholders: if sample image is still 404, run with --extract OR"
echo "   open https://yguard.ir/apps/inventory/admin → Extract models"
echo "   (writes into /opt/5stack/models/cs2-model-extract/images)."
echo "2) Skins missing in-game: on each game node ensure CSS InventorySimulator is loaded"
echo "   and YGuardInvSim 1.1+ is in custom-plugins. Also create:"
echo "     echo 'YOUR_KEY' > /opt/5stack/custom-plugins/invsim_apikey"
echo "   Key from Inventory Admin → Server key. Then restart dedicated servers."
echo "   Required cvars (YGuardInvSim sets these on boot):"
echo '     invsim_url "https://inventory.yguard.ir"'
echo "     invsim_require_inventory 1"
echo "     invsim_ws_enabled 1"
echo "     invsim_ws_immediately 1"
echo "3) Hard-refresh /apps/inventory (Ctrl+F5) after extract finishes."
