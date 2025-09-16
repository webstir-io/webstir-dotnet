FROM alpine:3.19

ARG MKCERT_VERSION=v1.4.4

RUN apk add --no-cache curl ca-certificates nss-tools openssl && \
    update-ca-certificates

# Download mkcert static binary
RUN curl -L -o /usr/local/bin/mkcert \
    https://github.com/FiloSottile/mkcert/releases/download/${MKCERT_VERSION}/mkcert-${MKCERT_VERSION}-linux-amd64 && \
    chmod +x /usr/local/bin/mkcert

ENV OUT=/out

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
