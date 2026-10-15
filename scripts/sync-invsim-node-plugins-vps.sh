#!/usr/bin/env bash
# 5stack dedicated pods often symlink gamedata from /opt/node-plugins (not
# custom-plugins). Sync InventorySimulator v3.7.0 + gamedata into every place
# the game process can load from.
#
# Run on GAME NODE (TG-60099):
#   bash /tmp/sync-invsim-node-plugins-vps.sh
set -euo pipefail

SRC_ZIP=/tmp/InventorySimulator-v3.7.0.zip
SRC_DIR=/tmp/invsim370
URL='https://github.com/ianlucas/cs2-css-inventory-simulator/releases/download/3.7.0/InventorySimulator-v3.7.0.zip'

if [[ ! -f "$SRC_DIR/addons/counterstrikesharp/plugins/InventorySimulator/InventorySimulator.dll" ]]; then
  wget -O "$SRC_ZIP" "$URL"
  rm -rf "$SRC_DIR"
  mkdir -p "$SRC_DIR"
  unzip -o "$SRC_ZIP" -d "$SRC_DIR"
fi

PLUGIN_SRC="$SRC_DIR/addons/counterstrikesharp/plugins/InventorySimulator"
GD_SRC="$SRC_DIR/addons/counterstrikesharp/gamedata/inventory-simulator.json"

sync_plugin() {
  local dest="$1"
  mkdir -p "$dest"
  cp -a "$PLUGIN_SRC"/. "$dest"/
  echo "plugin -> $dest ($(stat -c%s "$dest/InventorySimulator.dll") bytes)"
}

sync_gd() {
  local dest_dir="$1"
  mkdir -p "$dest_dir"
  cp -f "$GD_SRC" "$dest_dir/inventory-simulator.json"
  echo "gamedata -> $dest_dir/inventory-simulator.json"
}

# Node-wide overlays
sync_plugin /opt/5stack/custom-plugins/addons/counterstrikesharp/plugins/InventorySimulator
sync_gd /opt/5stack/custom-plugins/addons/counterstrikesharp/gamedata

if [[ -d /opt/node-plugins ]]; then
  sync_plugin /opt/node-plugins/addons/counterstrikesharp/plugins/InventorySimulator
  sync_gd /opt/node-plugins/addons/counterstrikesharp/gamedata
else
  echo "WARN: /opt/node-plugins missing"
fi

if [[ -d /opt/5stack/hosted-template/counterstrikesharp ]]; then
  sync_plugin /opt/5stack/hosted-template/counterstrikesharp/plugins/InventorySimulator
  mkdir -p /opt/5stack/hosted-template/counterstrikesharp/gamedata
  cp -f "$GD_SRC" /opt/5stack/hosted-template/counterstrikesharp/gamedata/inventory-simulator.json
fi

# Every dedicated server tree (public + hosted)
for d in /opt/5stack/servers/*/counterstrikesharp; do
  [[ -d "$d" ]] || continue
  sync_plugin "$d/plugins/InventorySimulator"
  sync_gd "$d/gamedata"
done

# Keep YGuardInvSim key readable
if [[ -f /opt/5stack/custom-plugins/addons/counterstrikesharp/plugins/YGuardInvSim/invsim_apikey ]]; then
  chmod 644 /opt/5stack/custom-plugins/addons/counterstrikesharp/plugins/YGuardInvSim/invsim_apikey
fi

echo
echo "Done. From panel (ubuntu) delete dedicated pods again, then retest."
ls -la /opt/node-plugins/addons/counterstrikesharp/gamedata/inventory-simulator.json 2>/dev/null || true
ls -la /opt/node-plugins/addons/counterstrikesharp/plugins/InventorySimulator/InventorySimulator.dll 2>/dev/null || true
