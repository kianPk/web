#!/usr/bin/env bash
# Diagnose why InvSim skins fail on dedicated pods (no fragile RCON).
# Run on panel (ubuntu):
#   bash /tmp/diag-invsim-pods-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack

for POD in $(kubectl -n "$NS" get pods -o name | grep dedicated-server); do
  echo "########## $POD ##########"
  kubectl -n "$NS" exec "$POD" -- bash -lc '
    set +e
    echo "SERVER_TYPE=$SERVER_TYPE"
    echo "=== key next to plugin ==="
    KEYF=/opt/instance/game/csgo/addons/counterstrikesharp/plugins/YGuardInvSim/invsim_apikey
    if [[ -f "$KEYF" ]]; then
      echo "key_len=$(wc -c < "$KEYF") prefix=$(head -c 8 "$KEYF")"
    else
      echo "MISSING $KEYF"
    fi
    echo "=== dll dates ==="
    ls -la /opt/instance/game/csgo/addons/counterstrikesharp/plugins/YGuardInvSim/ \
           /opt/5stack/custom-plugins/addons/counterstrikesharp/plugins/YGuardInvSim/ 2>/dev/null
    echo "=== InventorySimulator files ==="
    ls -la /opt/instance/game/csgo/addons/counterstrikesharp/plugins/InventorySimulator/ 2>/dev/null | head
    ls -la /opt/instance/game/csgo/addons/counterstrikesharp/configs/plugins/InventorySimulator/ 2>/dev/null | head
    echo "=== InventorySimulator cfg/json ==="
    find /opt/instance/game/csgo/addons/counterstrikesharp -iname "*InventorySimulator*" \( -name "*.json" -o -name "*.cfg" -o -name "*.toml" \) 2>/dev/null | while read f; do
      echo "-- $f"
      head -c 800 "$f"; echo
    done
    echo "=== gamemode cfg invsim lines ==="
    grep -nH invsim /opt/instance/game/csgo/cfg/*server*.cfg /opt/instance/game/csgo/cfg/server.cfg 2>/dev/null | head -40
    echo "=== recent CSS / InvSim log ==="
    for d in /opt/instance/game/csgo/addons/counterstrikesharp/logs \
             /opt/instance/game/csgo/logs \
             /opt/instance/game/csgo/addons/counterstrikesharp/plugins/InventorySimulator; do
      [[ -d "$d" ]] || continue
      grep -iEh "YGuardInvSim|invsim|apikey|inventory|equipped|401|403|fail" "$d"/* 2>/dev/null | tail -30
    done
    echo "=== curl equipped from inside pod ==="
    # fake steamid just to see if API reachable; real paint needs player steamid
    curl -sS -m 5 -o /tmp/eq.json -w "http=%{http_code}\n" \
      https://inventory.yguard.ir/api/equipped/v5/76561198000000000 || echo curl_fail
    head -c 200 /tmp/eq.json 2>/dev/null; echo
  ' || echo "exec failed"
  echo
done
