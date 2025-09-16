Sandbox TLS Certificates
========================

This folder holds self-signed certificates for the Sandbox HTTPS server.

Generate once:
- ./Sandbox/generate-certs.sh

It creates:
- dev.crt — certificate
- dev.key — private key

These are mounted into the Nginx container at /etc/nginx/certs.

Note: Browsers will warn on self-signed certs. For a trusted local cert, use mkcert instead and place the resulting key/cert here with names dev.key/dev.crt.

