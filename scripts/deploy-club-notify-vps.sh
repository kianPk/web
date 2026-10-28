#!/usr/bin/env bash
# Club notify types + website_url, then roll api/hasura/web.
# Usage on control plane (after CI finishes):
#   API_SHA=... WEB_SHA=... bash scripts/deploy-club-notify-vps.sh
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"

API_SHA="${API_SHA:?set API_SHA to full git sha}"
WEB_SHA="${WEB_SHA:?set WEB_SHA to full git sha}"

SQL_FILE="$(mktemp)"
trap 'rm -f "$SQL_FILE"' EXIT

cat >"$SQL_FILE" <<'SQL'
-- Club in-app notification types + optional website on club socials.

INSERT INTO e_notification_types ("value", "description") VALUES
  ('ClubChallengeActivated', 'A club challenge became available for you'),
  ('ClubNewsPosted', 'Your club posted news'),
  ('ClubTournamentCreated', 'Your club opened a tournament')
ON CONFLICT ("value") DO UPDATE
  SET "description" = EXCLUDED."description";

ALTER TABLE public.clubs
  ADD COLUMN IF NOT EXISTS website_url text;

ALTER TABLE public.clubs
  DROP CONSTRAINT IF EXISTS clubs_website_url_len;
ALTER TABLE public.clubs
  ADD CONSTRAINT clubs_website_url_len
  CHECK (website_url IS NULL OR char_length(website_url) BETWEEN 8 AND 200);

COMMENT ON COLUMN public.clubs.website_url IS
  'Optional club website URL shown with social links.';
SQL

PGPOD=$(kubectl -n 5stack get pods -o name | grep -E 'timescaledb|postgres' | head -1 | cut -d/ -f2)
kubectl -n 5stack cp "$SQL_FILE" "$PGPOD:/tmp/club_notify_website.sql"
kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql -U postgres -d postgres -f /tmp/club_notify_website.sql' \
  || kubectl -n 5stack exec "$PGPOD" -- bash -lc 'psql "$POSTGRES_CONNECTION_STRING" -f /tmp/club_notify_website.sql'

echo "club notify/website migration applied"

kubectl -n 5stack set image deploy/api api="ghcr.io/kianpk/api:${API_SHA}"
kubectl -n 5stack set image deploy/hasura migrations="ghcr.io/kianpk/api:${API_SHA}"
kubectl -n 5stack set image deploy/web web="ghcr.io/kianpk/web:${WEB_SHA}"
kubectl -n 5stack patch deploy/api --type=json -p='[{"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"Always"}]'
kubectl -n 5stack patch deploy/web --type=json -p='[{"op":"replace","path":"/spec/template/spec/containers/0/imagePullPolicy","value":"Always"}]'
kubectl -n 5stack rollout status deploy/api --timeout=5m
kubectl -n 5stack rollout status deploy/web --timeout=5m
kubectl -n 5stack rollout status deploy/hasura --timeout=5m || true

echo "club notify deploy done api=${API_SHA} web=${WEB_SHA}"
