#!/usr/bin/env bash
# Ensure servers.hosted_section exists in Postgres AND is tracked in Hasura.
# Without Hasura tracking, the public Servers fleet GraphQL fails and every
# mode shows "No servers online" even though Dedicated Servers lists them.
#
# Usage on the panel VPS:
#   bash scripts/fix-hosted-section-hasura-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack

SQL_FILE="$(mktemp)"
META_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE" "$META_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
ALTER TABLE public.servers
  ADD COLUMN IF NOT EXISTS hosted_section boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN public.servers.hosted_section IS
  'Paid Host rental with section_mode — excluded from the public fleet.';

SELECT column_name, data_type, column_default
  FROM information_schema.columns
 WHERE table_schema = 'public' AND table_name = 'servers'
   AND column_name IN ('section_mode', 'hosted_section')
 ORDER BY column_name;
SQL

PGPOD=$(kubectl -n "$NS" get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
echo "postgres: $PGPOD"
kubectl -n "$NS" cp "$SQL_FILE" "$PGPOD:/tmp/hosted_section.sql"
kubectl -n "$NS" exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/hosted_section.sql' \
  || kubectl -n "$NS" exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/hosted_section.sql'

HASURA_POD=$(kubectl -n "$NS" get pods -o name | grep -E 'hasura' | head -1 | cut -d/ -f2)
echo "hasura: $HASURA_POD"
ADMIN_SECRET=$(kubectl -n "$NS" get secret -o name | grep -E 'hasura' | head -1 | xargs -I{} kubectl -n "$NS" get {} -o jsonpath='{.data.HASURA_GRAPHQL_ADMIN_SECRET}' | base64 -d)
if [[ -z "${ADMIN_SECRET}" ]]; then
  ADMIN_SECRET=$(kubectl -n "$NS" get deploy hasura -o jsonpath='{.spec.template.spec.containers[0].env[?(@.name=="HASURA_GRAPHQL_ADMIN_SECRET")].value}' 2>/dev/null || true)
fi
if [[ -z "${ADMIN_SECRET}" ]]; then
  echo "WARN: could not read HASURA_GRAPHQL_ADMIN_SECRET — restart hasura after metadata apply from git"
  kubectl -n "$NS" rollout restart deploy/hasura 2>/dev/null || true
  exit 0
fi

# Track column + refresh select permissions for roles that read servers.
cat >"$META_FILE" <<'JSON'
{
  "type": "bulk",
  "args": [
    {
      "type": "pg_track_table",
      "args": {
        "source": "default",
        "table": { "schema": "public", "name": "servers" },
        "if_exists": true
      }
    },
    {
      "type": "pg_add_column_to_table",
      "args": {
        "source": "default",
        "table": { "schema": "public", "name": "servers" },
        "name": "hosted_section"
      }
    }
  ]
}
JSON

# Prefer drop-in permission patch via run_sql + reload_metadata (works even when
# pg_add_column_to_table is unavailable on older Hasura builds).
kubectl -n "$NS" exec "$HASURA_POD" -- bash -lc "
set -e
curl -sS -X POST http://127.0.0.1:8080/v1/metadata \
  -H 'Content-Type: application/json' \
  -H \"x-hasura-admin-secret: ${ADMIN_SECRET}\" \
  -d '{\"type\":\"pg_track_column\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"servers\"},\"name\":\"hosted_section\"}}' || true
curl -sS -X POST http://127.0.0.1:8080/v1/metadata \
  -H 'Content-Type: application/json' \
  -H \"x-hasura-admin-secret: ${ADMIN_SECRET}\" \
  -d '{\"type\":\"reload_metadata\",\"args\":{\"reload_remote_schemas\":true}}'
"

echo "=== GraphQL smoke (admin) ==="
kubectl -n "$NS" exec "$HASURA_POD" -- bash -lc "
curl -sS -X POST http://127.0.0.1:8080/v1/graphql \
  -H 'Content-Type: application/json' \
  -H \"x-hasura-admin-secret: ${ADMIN_SECRET}\" \
  -d '{\"query\":\"{ servers(where:{section_mode:{_is_null:false}}, limit:3) { id label section_mode hosted_section connected } }\"}'
"

echo ""
echo "DONE — hard-refresh /game-servers/duels. If hosted_section still errors for guest,"
echo "re-apply Hasura metadata from the api repo (public_servers.yaml already lists the column)."
