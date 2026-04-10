#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"

if ! command -v node &>/dev/null; then
  echo "Node.js not found. Install from https://nodejs.org (LTS)"
  exit 1
fi

[ ! -d node_modules ] && npm install
[ ! -d public ]       && mkdir public

if [ ! -f public/RandomOverlay.html ]; then
  echo "WARNING: public/RandomOverlay.html not found. Copy it there before using."
fi

echo "Starting BLT Overlay Server…"
node server.js