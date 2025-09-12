ARG NGINX_VERSION=1.27.1
ARG NGINX_RUNTIME_TAG=alpine

FROM alpine:3.20 AS builder

ARG NGINX_VERSION
RUN apk add --no-cache \
    build-base \
    pcre2-dev \
    zlib-dev \
    openssl-dev \
    linux-headers \
    git \
    wget \
    ca-certificates

WORKDIR /tmp
RUN wget -q https://nginx.org/download/nginx-${NGINX_VERSION}.tar.gz \
    && tar xzf nginx-${NGINX_VERSION}.tar.gz \
    && git clone --recursive https://github.com/google/ngx_brotli.git

WORKDIR /tmp/nginx-${NGINX_VERSION}
RUN ./configure --with-compat --add-dynamic-module=../ngx_brotli \
    && make modules


FROM nginx:${NGINX_RUNTIME_TAG}

ARG NGINX_VERSION
# Copy compiled Brotli dynamic modules
COPY --from=builder /tmp/nginx-${NGINX_VERSION}/objs/ngx_http_brotli_filter_module.so /etc/nginx/modules/
COPY --from=builder /tmp/nginx-${NGINX_VERSION}/objs/ngx_http_brotli_static_module.so /etc/nginx/modules/

# Provide a main nginx.conf that loads modules and includes site configs
COPY nginx.main.conf /etc/nginx/nginx.conf

# Default document root is /usr/share/nginx/html (used by compose volume mounts)
WORKDIR /usr/share/nginx/html
