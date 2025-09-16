#!/bin/sh
set -eu

# Generate dev certs using mkcert into $OUT (mounted volume)
OUT_DIR="${OUT:-/out}"
mkdir -p "$OUT_DIR"

CRT="$OUT_DIR/dev.crt"
KEY="$OUT_DIR/dev.key"

if [ ! -f "$CRT" ] || [ ! -f "$KEY" ]; then
  echo "[mkcert] Generating certs in $OUT_DIR"
  export CAROOT="$OUT_DIR"
  # Create CA in CAROOT if missing and generate cert
  mkcert -cert-file "$CRT" -key-file "$KEY" localhost 127.0.0.1 web
  echo "[mkcert] Created $CRT and $KEY"
else
  echo "[mkcert] Certs already exist, skipping"
fi

echo "[mkcert] Done"
