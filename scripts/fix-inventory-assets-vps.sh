#!/usr/bin/env bash
# Diagnose why inventory.yguard.ir shows placeholder skin art and why
# InvSim may not paint weapons in-game.
#
# Run on the panel VPS:
#   bash scripts/fix-inventory-assets-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack

echo "=== inventory pods ==="
kubectl -n "$NS" get pods -o wide | grep -E 'NAME|inventory' || true

echo
echo "=== hostPath extract tree ==="
# Upstream expects /opt/5stack/models/cs2-model-extract on the node.
if [[ -d /opt/5stack/models/cs2-model-extract ]]; then
  echo "host: /opt/5stack/models/cs2-model-extract"
  du -sh /opt/5stack/models/cs2-model-extract/* 2>/dev/null | head -20 || true
  echo "images count: $(find /opt/5stack/models/cs2-model-extract/images -type f 2>/dev/null | wc -l)"
  echo "paints count: $(find /opt/5stack/models/cs2-model-extract/paints -type f 2>/dev/null | wc -l)"
  ls /opt/5stack/models/cs2-model-extract/images 2>/dev/null | head -5 || echo "(no images/)"
else
  echo "MISSING hostPath /opt/5stack/models/cs2-model-extract"
  echo "Create it and run inventory Admin → Extract models, or:"
  echo "  kubectl -n $NS exec deploy/inventory-backend -- ls -la /cs2-models /cs2-game 2>&1 | head"
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
    ls -la /cs2-models /cs2-game 2>&1 | head -30
    echo "images: $(find /cs2-models/images -type f 2>/dev/null | wc -l)"
  ' || true
  echo
  echo "=== recent backend logs ==="
  kubectl -n "$NS" logs "$BE" --tail=80 2>/dev/null || true
else
  echo "(no inventory-backend pod)"
fi

echo
echo "=== public probes ==="
curl -sS -o /dev/null -w "catalog %{http_code}\n" https://inventory.yguard.ir/api/catalog || true
SAMPLE=$(curl -sS https://inventory.yguard.ir/api/catalog | sed -n 's/.*"image":"\([^"]*weapon_ak47[^"]*\)".*/\1/p' | head -1)
if [[ -n "$SAMPLE" ]]; then
  echo "sample image path: $SAMPLE"
  curl -sS -o /dev/null -w "sample image %{http_code}\n" "https://inventory.yguard.ir${SAMPLE}" || true
else
  echo "could not pick a sample image from catalog"
  curl -sS -o /dev/null -w "weapon_ak47 hardcoded %{http_code}\n" \
    https://inventory.yguard.ir/images/weapon_ak47_4797ec49.webp || true
fi
curl -sS -o /dev/null -w "equipped/v5 %{http_code}\n" \
  https://inventory.yguard.ir/api/equipped/v5/76561198000000000 || true

echo
echo "=== NEXT STEPS ==="
echo "1) UI placeholders (blurry circles): open https://yguard.ir/apps/inventory/admin as admin"
echo "   → Extract / rebuild asset cache (writes into /cs2-models/images)."
echo "   Or from backend pod, run the extract script against /cs2-game."
echo "2) Skins missing in-game: on each game node ensure CSS InventorySimulator is loaded"
echo "   and cfg has:"
echo '     invsim_url "https://inventory.yguard.ir"'
echo '     invsim_apikey "<from inventory Admin → Server key>"'
echo "     invsim_ws_enabled 1"
echo "     invsim_ws_immediately 1"
echo "     invsim_require_inventory 1"
echo "   YGuardInvSim only sets url/ws flags — apikey + require_inventory must also be set."
echo "3) After extract, hard-refresh /apps/inventory (Ctrl+F5)."
