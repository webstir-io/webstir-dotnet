Sandbox TLS Certificates
========================

This folder holds self-signed certificates for the Sandbox HTTPS server.

## Generate Certificates (mkcert container)

From the repo root, run:

```bash
docker compose -f Sandbox/docker-compose.yml run --rm certs
```

This uses the `mkcert` container to:
- Create a local CA in `Sandbox/certs` (`rootCA.pem` and `rootCA-key.pem`).
- Generate `dev.crt` (certificate) and `dev.key` (private key) for `localhost`, `127.0.0.1`, and `web`.

These files are mounted into the Nginx container at `/etc/nginx/certs` by `Sandbox/docker-compose.yml`.

## Trust the Local CA

To trust the new root CA on your host (so browsers stop warning on the Sandbox HTTPS endpoint), run:

```bash
Sandbox/mkcert/trust-local-ca.sh
```

This script imports `Sandbox/certs/rootCA.pem` into the OS trust store on supported platforms (macOS and common Linux distributions). On unsupported platforms, import `rootCA.pem` manually.

Note: Private keys (`dev.key`, `rootCA-key.pem`) are intentionally ignored by git and should remain local only.
