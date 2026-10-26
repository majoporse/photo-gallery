#!/bin/sh
# Optional demo content: one collection and ten real photographs, uploaded
# through the same admin API the UI uses.
#
# The images come from Lorem Picsum (picsum.photos), which serves Unsplash
# photographs for exactly this purpose. Downloading needs network access; if it
# is unavailable the script falls back to generated gradients so a seed never
# silently produces an empty gallery.
set -eu

API="${API_URL:-http://api:8080}"
USERNAME="${ADMIN_USERNAME:-admin}"
PASSWORD="${ADMIN_PASSWORD:-dev-password}"
COOKIES=/tmp/cookies
COUNT=10

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
  -d '{"name":"Sample","slug":"sample","description":"Photographs from Lorem Picsum, uploaded by the seed profile."}'

COLLECTION_ID=$(curl -fsS -b "$COOKIES" -H 'X-Requested-With: photo-gallery' \
  "$API/api/v1/admin/collections" | jq -r '.[] | select(.slug == "sample") | .id')

if [ -z "$COLLECTION_ID" ] || [ "$COLLECTION_ID" = "null" ]; then
  echo "could not resolve the sample collection" >&2
  exit 1
fi

echo "fetching $COUNT photographs"
index=1
while [ "$index" -le "$COUNT" ]; do
  target="/tmp/photo-$index.jpg"
  # The seed makes the selection reproducible across runs.
  if ! curl -fsSL --max-time 40 \
    "https://picsum.photos/seed/gallery-$index/1600/1067" -o "$target"; then
    echo "  photo $index: download failed, generating a gradient instead"
    magick -size 1600x1067 gradient:'#2b3a4a'-'#c8b8a0' "$target"
  fi
  index=$((index + 1))
done

echo "uploading"
set --
for file in /tmp/photo-*.jpg; do
  set -- "$@" -F "files=@$file"
done

UPLOAD=$(curl -fsS -b "$COOKIES" \
  -X POST "$API/api/v1/admin/collections/$COLLECTION_ID/photos" \
  -H 'X-Requested-With: photo-gallery' \
  "$@")

ADDED=$(echo "$UPLOAD" | jq -r '.created | length')
REJECTED=$(echo "$UPLOAD" | jq -r '.failed | length')
echo "  $ADDED added, $REJECTED rejected"

# Feature the first photo so the home page carousel has something to show.
PHOTO_ID=$(echo "$UPLOAD" | jq -r '.created[0].id // empty')

if [ -n "$PHOTO_ID" ]; then
  curl -fsS -b "$COOKIES" -o /dev/null \
    -X PATCH "$API/api/v1/admin/photos/$PHOTO_ID" \
    -H 'Content-Type: application/json' \
    -H 'X-Requested-With: photo-gallery' \
    -d '{"title":"Picsum study","altText":"A photograph from Lorem Picsum, used as demo content.","featuredRank":0}' \
    || true
fi

echo "seeded http://localhost:3000/collections/sample"
