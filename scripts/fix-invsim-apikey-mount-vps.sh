#!/usr/bin/env bash
# Dedicated pods do NOT mount /opt/5stack/custom-plugins/invsim_apikey into the
# container (only the addons/ tree is merged into /opt/instance). Copy the key
# next to YGuardInvSim.dll and inject invsim_* into shared gamemode cfgs.
#
# Run on the GAME NODE (TG-60099):
#   bash /tmp/fix-invsim-apikey-mount-vps.sh
set -euo pipefail

KEY_SRC=/opt/5stack/custom-plugins/invsim_apikey
KEY_DST=/opt/5stack/custom-plugins/addons/counterstrikesharp/plugins/YGuardInvSim/invsim_apikey
PLUGIN_DIR=/opt/5stack/custom-plugins/addons/counterstrikesharp/plugins/YGuardInvSim
CFG_DIR=/opt/5stack/custom-plugins/cfg

if [[ ! -f "$KEY_SRC" ]]; then
  echo "FATAL: missing $KEY_SRC — paste real inv_ key there first"
  exit 1
fi
KEY=$(tr -d '\r\n' < "$KEY_SRC")
if [[ "$KEY" == *YOUR_KEY* || "$KEY" == *XXXX* || "$KEY" == *a1b2c3* || ${#KEY} -lt 20 ]]; then
  echo "FATAL: $KEY_SRC still looks like a placeholder (${#KEY} chars)"
  exit 1
fi

mkdir -p "$PLUGIN_DIR" "$CFG_DIR"
printf '%s\n' "$KEY" > "$KEY_DST"
chmod 600 "$KEY_DST"
echo "wrote $KEY_DST"

BLOCK=$(cat <<EOF

// YGuard InvSim (auto — skins on public/DM dedicated)
invsim_url "https://inventory.yguard.ir"
invsim_require_inventory 1
invsim_ws_enabled 1
invsim_ws_immediately 1
invsim_spraychanger_enabled 1
invsim_apikey "$KEY"
EOF
)

for f in \
  "$CFG_DIR/gamemode_casual_server.cfg" \
  "$CFG_DIR/gamemode_competitive_server.cfg" \
  "$CFG_DIR/gamemode_deathmatch_server.cfg" \
  "$CFG_DIR/server.cfg"
do
  touch "$f"
  # strip previous auto block then append
  if grep -q 'YGuard InvSim (auto' "$f" 2>/dev/null; then
    sed -i '/YGuard InvSim (auto/,/invsim_apikey /d' "$f" || true
  fi
  printf '%s\n' "$BLOCK" >> "$f"
  echo "updated $f"
done

# also per-server public templates if present
for f in /opt/5stack/servers/*/cfg/public.server.cfg \
         /opt/5stack/servers/*/cfg/server.cfg; do
  [[ -f "$f" ]] || continue
  if grep -q 'YGuard InvSim (auto' "$f" 2>/dev/null; then
    sed -i '/YGuard InvSim (auto/,/invsim_apikey /d' "$f" || true
  fi
  printf '%s\n' "$BLOCK" >> "$f"
  echo "updated $f"
done

echo
echo "Done. Restart all dedicated servers from the panel, then rejoin a public/DM server."
echo "Verify inside a pod: cat /opt/instance/game/csgo/addons/counterstrikesharp/plugins/YGuardInvSim/invsim_apikey"
