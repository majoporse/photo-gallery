#!/bin/sh
# Optional demo content: one collection, two generated photographs, uploaded
# through the same admin API the UI uses.
set -eu

API="${API_URL:-http://api:8080}"
USERNAME="${ADMIN_USERNAME:-admin}"
PASSWORD="${ADMIN_PASSWORD:-dev-password}"
COOKIES=/tmp/cookies

echo "waiting for the API"
until curl -fsS "$API/api/v1/health/ready" >/dev/null 2>&1; do
  sleep 1
done

echo "signing in"
curl -fsS -c "$COOKIES" -o /dev/null \
  -X POST "$API/api/v1/admin/session" \
  -H 'Content-Type: application/json' \
  -H 'X-Requested-With: photo-gallery' \
  -d "{\"username\":\"$USERNAME\",\"password\":\"$PASSWORD\"}"

echo "creating the sample collection"
curl -sS -b "$COOKIES" -o /dev/null \
  -X POST "$API/api/v1/admin/collections" \
  -H 'Content-Type: application/json' \
  -H 'X-Requested-With: photo-gallery' \
  -d '{"name":"Sample","slug":"sample","description":"Generated locally by the seed profile."}'

COLLECTION_ID=$(curl -fsS -b "$COOKIES" -H 'X-Requested-With: photo-gallery' \
  "$API/api/v1/admin/collections" | jq -r '.[] | select(.slug == "sample") | .id')

if [ -z "$COLLECTION_ID" ] || [ "$COLLECTION_ID" = "null" ]; then
  echo "could not resolve the sample collection" >&2
  exit 1
fi

magick -size 1600x1000 gradient:'#1b2a41'-'#c9b79c' /tmp/one.jpg
magick -size 1600x1000 gradient:'#3a2f28'-'#e8e1d5' /tmp/two.jpg

echo "uploading"
curl -fsS -b "$COOKIES" -o /dev/null \
  -X POST "$API/api/v1/admin/collections/$COLLECTION_ID/photos" \
  -H 'X-Requested-With: photo-gallery' \
  -F 'files=@/tmp/one.jpg' \
  -F 'files=@/tmp/two.jpg'

echo "seeded http://localhost:3000/collections/sample"
