#!/usr/bin/env bash
# After a domestic node has joined k3s, copy CS files + import images + drop plugins.
#
# Usage on panel VPS:
#   DONOR_NODE=<existing-online-node-id> \
#   NEW_NODE=<new-node-id> \
#   bash scripts/seed-domestic-node-vps.sh
#
# Requires SSH root access from the panel to both nodes (same LAN / WireGuard),
# or set DONOR_HOST / NEW_HOST to reachable IPs.
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

KIT_ROOT="${KIT_ROOT:-/opt/5stack/domestic-kit}"
DONOR_NODE="${DONOR_NODE:?set DONOR_NODE to an existing Online node name}"
NEW_NODE="${NEW_NODE:?set NEW_NODE to the new node name}"

node_ip() {
  kubectl get node "$1" -o jsonpath='{.status.addresses[?(@.type=="InternalIP")].address}' 2>/dev/null \
    || kubectl get node "$1" -o jsonpath='{.status.addresses[0].address}'
}

DONOR_HOST="${DONOR_HOST:-$(node_ip "$DONOR_NODE")}"
NEW_HOST="${NEW_HOST:-$(node_ip "$NEW_NODE")}"

if [[ -z "$DONOR_HOST" || -z "$NEW_HOST" ]]; then
  echo "Could not resolve node IPs. Pass DONOR_HOST=… NEW_HOST=…" >&2
  exit 1
fi

SSH=(ssh -o StrictHostKeyChecking=no -o BatchMode=yes -o ConnectTimeout=15)

echo "==> wait for node Ready: $NEW_NODE"
kubectl wait --for=condition=Ready "node/${NEW_NODE}" --timeout=10m

echo "==> import kit images onto ${NEW_HOST}"
if [[ -d "${KIT_ROOT}/images" ]]; then
  for tar in "${KIT_ROOT}/images"/*.tar; do
    [[ -f "$tar" ]] || continue
    echo "    $(basename "$tar")"
    cat "$tar" | "${SSH[@]}" "root@${NEW_HOST}" "ctr -n k8s.io images import -"
  done
  if [[ -f "${KIT_ROOT}/images/k3s-airgap-images-amd64.tar.gz" ]]; then
    echo "    (k3s airgap system images already applied at join time if used)"
  fi
fi

echo "==> copy plugins kit -> ${NEW_HOST}:/opt/5stack/custom-plugins"
"${SSH[@]}" "root@${NEW_HOST}" "mkdir -p /opt/5stack/custom-plugins /opt/5stack/serverfiles /opt/5stack/steamcmd /opt/5stack/demos"
if [[ -d "${KIT_ROOT}/plugins" ]] && ls "${KIT_ROOT}/plugins"/*.zip >/dev/null 2>&1; then
  # Connector expects extracted layouts under custom-plugins; drop zips for API
  # re-sync, and also rsync whatever the donor already extracted.
  scp -o StrictHostKeyChecking=no -o BatchMode=yes \
    "${KIT_ROOT}/plugins"/*.zip "root@${NEW_HOST}:/opt/5stack/custom-plugins/" || true
fi

echo "==> rsync CS serverfiles donor ${DONOR_HOST} -> ${NEW_HOST}"
# Pipe through panel so donor/new do not need direct SSH to each other.
"${SSH[@]}" "root@${DONOR_HOST}" \
  "tar -C /opt/5stack -cf - serverfiles steamcmd custom-plugins 2>/dev/null" \
  | "${SSH[@]}" "root@${NEW_HOST}" "tar -C /opt/5stack -xf -"

echo "==> nudge plugin sync on connector (if up)"
NEW_IP="$NEW_HOST"
curl -fsS -m 10 "http://${NEW_IP}:8585/plugins/sync" >/dev/null 2>&1 || true

echo "==> patch connector pull policy IfNotPresent"
kubectl -n 5stack patch ds game-server-node-connector --type=json \
  -p='[{"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"IfNotPresent"}]' 2>/dev/null || true

echo "seed done: ${NEW_NODE} from donor ${DONOR_NODE}"
echo "In panel: set node Online if needed, then create dedicated/match servers as usual."
echo "Tip: pin plugin runtime versions (v*) so game pods use imagePullPolicy IfNotPresent."
