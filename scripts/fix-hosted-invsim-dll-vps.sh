#!/usr/bin/env bash
# Hosted pods were still loading InventorySimulator.dll 79360 (Sep 24) into
# /opt/instance while /opt/node-plugins already had 96256 (v3.7.0).
# Find every copy on the game node + force-overwrite inside running pods.
#
# Part A — game node (TG-60099):
#   bash /tmp/fix-hosted-invsim-dll-vps.sh --host
# Part B — panel (ubuntu):
#   bash /tmp/fix-hosted-invsim-dll-vps.sh --pods
set -euo pipefail

MODE="${1:---host}"
GOOD_SIZE=96256
SRC_ZIP=/tmp/InventorySimulator-v3.7.0.zip
URL='https://github.com/ianlucas/cs2-css-inventory-simulator/releases/download/3.7.0/InventorySimulator-v3.7.0.zip'

if [[ "$MODE" == "--host" ]]; then
  echo "=== all InventorySimulator.dll on this node ==="
  find /opt/5stack /opt/node-plugins /var/lib/rancher /opt -name 'InventorySimulator.dll' 2>/dev/null \
    | while read -r f; do ls -la "$f"; done || true

  if [[ ! -f /tmp/invsim370/addons/counterstrikesharp/plugins/InventorySimulator/InventorySimulator.dll ]]; then
    wget -O "$SRC_ZIP" "$URL"
    rm -rf /tmp/invsim370 && mkdir -p /tmp/invsim370
    unzip -o "$SRC_ZIP" -d /tmp/invsim370
  fi
  GOOD=/tmp/invsim370/addons/counterstrikesharp/plugins/InventorySimulator
  GD=/tmp/invsim370/addons/counterstrikesharp/gamedata/inventory-simulator.json

  echo
  echo "=== overwrite every InventorySimulator tree under /opt/5stack ==="
  find /opt/5stack -type d -name InventorySimulator 2>/dev/null | while read -r d; do
    if [[ -f "$d/InventorySimulator.dll" ]] || [[ "$d" == */plugins/InventorySimulator ]]; then
      cp -a "$GOOD"/. "$d"/
      echo "fixed $d ($(stat -c%s "$d/InventorySimulator.dll"))"
    fi
  done

  echo
  echo "=== gamedata ==="
  find /opt/5stack -path '*/gamedata' -type d 2>/dev/null | while read -r d; do
    cp -f "$GD" "$d/inventory-simulator.json"
    echo "gd $d"
  done

  # If node-plugins exists as a real dir on host
  if [[ -d /opt/node-plugins ]]; then
    mkdir -p /opt/node-plugins/addons/counterstrikesharp/plugins/InventorySimulator
    mkdir -p /opt/node-plugins/addons/counterstrikesharp/gamedata
    cp -a "$GOOD"/. /opt/node-plugins/addons/counterstrikesharp/plugins/InventorySimulator/
    cp -f "$GD" /opt/node-plugins/addons/counterstrikesharp/gamedata/inventory-simulator.json
  fi

  echo "Host done. Run on panel: bash $0 --pods"
  exit 0
fi

if [[ "$MODE" == "--pods" ]]; then
  export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
  NS=5stack
  for POD in $(kubectl -n "$NS" get pods -o name | grep dedicated-server); do
    echo "########## $POD ##########"
    kubectl -n "$NS" exec "$POD" -- bash -lc '
      set -e
      GOOD=/opt/node-plugins/addons/counterstrikesharp/plugins/InventorySimulator
      DEST=/opt/instance/game/csgo/addons/counterstrikesharp/plugins/InventorySimulator
      GD_SRC=/opt/custom-plugins/addons/counterstrikesharp/gamedata/inventory-simulator.json
      GD_ALT=/opt/node-plugins/addons/counterstrikesharp/gamedata/inventory-simulator.json
      if [[ -f "$GOOD/InventorySimulator.dll" ]]; then
        cp -a "$GOOD"/. "$DEST"/
        echo "instance dll now $(stat -c%s "$DEST/InventorySimulator.dll")"
      else
        echo "no good dll at $GOOD"
      fi
      # refresh gamedata file behind symlink if possible
      for g in "$GD_SRC" "$GD_ALT"; do
        if [[ -f "$g" && ! -L "$g" ]]; then
          echo "gamedata host file $g $(stat -c%s "$g")"
        fi
      done
      ls -la "$DEST/InventorySimulator.dll"
    ' || echo "exec failed"
  done
  echo
  echo "Optional: css_plugins reload via RCON, or delete pods once more."
  exit 0
fi

echo "Usage: $0 --host | --pods"
exit 1
