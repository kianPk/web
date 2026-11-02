#!/usr/bin/env bash
# Build an offline/domestic node kit on the control plane WHILE international
# internet still works. New Iranian nodes can then join without GitHub/GHCR.
#
# Usage on panel VPS:
#   bash scripts/prepare-domestic-node-kit-vps.sh
#
# Output: /opt/5stack/domestic-kit/{k3s,images,plugins,manifest.json}
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

KIT_ROOT="${KIT_ROOT:-/opt/5stack/domestic-kit}"
ARCH="${ARCH:-amd64}"
K3S_VERSION="${K3S_VERSION:-$(k3s --version 2>/dev/null | awk '{print $3}' | head -1 || true)}"
if [[ -z "${K3S_VERSION}" ]]; then
  K3S_VERSION="v1.31.3+k3s1"
fi

mkdir -p "$KIT_ROOT"/{k3s,images,plugins,bin}
cd "$KIT_ROOT"

echo "==> k3s airgap (${K3S_VERSION})"
curl -fsSL "https://get.k3s.io" -o k3s/install.sh
chmod +x k3s/install.sh
# GitHub release assets for the binary + airgap system images
BASE="https://github.com/k3s-io/k3s/releases/download/${K3S_VERSION}"
curl -fL "${BASE}/k3s" -o "bin/k3s"
chmod +x bin/k3s
curl -fL "${BASE}/k3s-airgap-images-${ARCH}.tar.gz" -o "images/k3s-airgap-images-${ARCH}.tar.gz"

echo "==> export running 5stack images from this cluster"
IMAGES=()
while IFS= read -r img; do
  [[ -n "$img" ]] && IMAGES+=("$img")
done < <(
  kubectl -n 5stack get deploy,ds,sts -o jsonpath='{range .items[*]}{range .spec.template.spec.containers[*]}{.image}{"\n"}{end}{end}' 2>/dev/null \
    | grep -E 'game-server-node-connector|game-server-|gamedata-validator|game-streamer' \
    | sort -u
)

# Always try common connector tags even if not listed
IMAGES+=(
  "ghcr.io/5stackgg/game-server-node-connector:latest"
)

# Deduplicate
mapfile -t IMAGES < <(printf '%s\n' "${IMAGES[@]}" | awk 'NF' | sort -u)

for img in "${IMAGES[@]}"; do
  safe=$(echo "$img" | tr '/:' '__')
  out="images/${safe}.tar"
  echo "    saving $img -> $out"
  if command -v ctr >/dev/null 2>&1; then
    ctr -n k8s.io images pull "$img" || true
    ctr -n k8s.io images export "$out" "$img" || docker save -o "$out" "$img"
  else
    docker pull "$img"
    docker save -o "$out" "$img"
  fi
done

echo "==> download enabled plugin archives (best-effort)"
# Prefer API if reachable in-cluster; otherwise skip (prepare can re-run later)
API_POD=$(kubectl -n 5stack get pods -l app=api -o jsonpath='{.items[0].metadata.name}' 2>/dev/null || true)
if [[ -n "${API_POD}" ]]; then
  kubectl -n 5stack exec "$API_POD" -- node -e '
    const { Client } = require("pg");
    (async () => {
      const c = new Client({ connectionString: process.env.POSTGRES_CONNECTION_STRING || process.env.DATABASE_URL });
      await c.connect();
      const r = await c.query(`
        SELECT DISTINCT ON (v.plugin_slug) v.plugin_slug, v.version, v.url, v.sha256
        FROM public.game_plugin_versions v
        JOIN public.game_plugin_installs i ON i.plugin_slug = v.plugin_slug AND i.enabled = true
        WHERE v.url IS NOT NULL AND v.sha256 IS NOT NULL
        ORDER BY v.plugin_slug, v.published_at DESC
      `);
      for (const row of r.rows) {
        console.log([row.sha256, row.url, row.plugin_slug, row.version].join("\t"));
      }
      await c.end();
    })().catch((e) => { console.error(e); process.exit(1); });
  ' 2>/dev/null | while IFS=$'\t' read -r sha url slug ver; do
    [[ -z "$sha" || -z "$url" ]] && continue
    dest="plugins/${sha}.zip"
    if [[ -f "$dest" ]]; then
      echo "    skip $slug@$ver (cached)"
      continue
    fi
    echo "    $slug@$ver"
    if ! curl -fL --retry 3 -A "5stack-domestic-kit" -o "$dest" "$url"; then
      # try common GH mirrors
      for mirror in "https://ghfast.top/" "https://mirror.ghproxy.com/"; do
        if curl -fL --retry 2 -A "5stack-domestic-kit" -o "$dest" "${mirror}${url}"; then
          break
        fi
      done
    fi
    if [[ ! -s "$dest" ]]; then
      echo "    WARN failed to fetch $slug" >&2
      rm -f "$dest"
    fi
  done || echo "    WARN plugin list query failed (non-fatal)"
fi

echo "==> write manifest"
cat > manifest.json <<EOF
{
  "prepared_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "k3s_version": "${K3S_VERSION}",
  "arch": "${ARCH}",
  "kit_root": "${KIT_ROOT}",
  "notes": [
    "Run seed-domestic-node-vps.sh after the new node joins to copy CS files from a donor node.",
    "Steam/GitHub are not required on the new node if this kit + donor CS seed are complete."
  ]
}
EOF

# Soften connector pulls so airgap imports work
kubectl -n 5stack patch ds game-server-node-connector --type=json \
  -p='[{"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"IfNotPresent"}]' 2>/dev/null || true
kubectl -n 5stack patch ds game-server-node-connector-nvidia --type=json \
  -p='[{"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"IfNotPresent"}]' 2>/dev/null || true

du -sh "$KIT_ROOT" "$KIT_ROOT"/images "$KIT_ROOT"/plugins 2>/dev/null || true
echo "domestic kit ready at ${KIT_ROOT}"
echo "Mount this path into the API pod as DOMESTIC_KIT_PATH=${KIT_ROOT} (hostPath) so join script + plugin cache work."
