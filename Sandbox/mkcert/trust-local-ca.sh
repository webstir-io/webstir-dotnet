#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root_dir="$(cd "${script_dir}/../.." && pwd)"
caroot_dir="${root_dir}/Sandbox/certs"
ca_pem="${caroot_dir}/rootCA.pem"
compose_file="${root_dir}/Sandbox/docker-compose.yml"

if [ ! -f "$ca_pem" ]; then
    echo "[trust] Root CA not found. Running mkcert container..."
    docker compose -f "$compose_file" run --rm certs
fi

if [ ! -f "$ca_pem" ]; then
    echo "[trust] Root CA still missing at $ca_pem" >&2
    exit 1
fi

os_name="$(uname -s)"
case "$os_name" in
    Darwin)
        fingerprint="$(openssl x509 -in "$ca_pem" -noout -fingerprint -sha1 | awk -F'=' '{print $2}' | tr -d ':')"
        if [ -z "$fingerprint" ]; then
            echo "[trust] Unable to read certificate fingerprint." >&2
            exit 1
        fi

        if security find-certificate -a -Z /Library/Keychains/System.keychain 2>/dev/null | grep -F "$fingerprint" >/dev/null 2>&1; then
            echo "[trust] Certificate already trusted in System keychain."
        else
            echo "[trust] Adding certificate to System keychain (sudo may prompt)..."
            sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain "$ca_pem"
            echo "[trust] Certificate trusted in System keychain."
        fi
        ;;
    Linux)
        if command -v update-ca-certificates >/dev/null 2>&1; then
            target="/usr/local/share/ca-certificates/webstir-sandbox.crt"
            if [ -f "$target" ] && cmp -s "$ca_pem" "$target"; then
                echo "[trust] Certificate already trusted."
            else
                echo "[trust] Installing certificate into /usr/local/share/ca-certificates (sudo may prompt)..."
                sudo install -m 0644 "$ca_pem" "$target"
                sudo update-ca-certificates
                echo "[trust] Certificate trusted."
            fi
        elif command -v trust >/dev/null 2>&1; then
            fingerprint="$(openssl x509 -in "$ca_pem" -noout -fingerprint -sha1)"
            if trust list | grep -F "$fingerprint" >/dev/null 2>&1; then
                echo "[trust] Certificate already trusted."
            else
                echo "[trust] Adding certificate using trust (sudo may prompt)..."
                sudo trust anchor "$ca_pem"
                echo "[trust] Certificate trusted."
            fi
        else
            echo "[trust] Unsupported Linux trust store. Import $ca_pem manually." >&2
        fi
        ;;
    *)
        echo "[trust] Automatic trust not supported on $os_name. Import $ca_pem manually." >&2
        ;;
esac
