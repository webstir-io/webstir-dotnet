# Multi-stage build for Nginx with Brotli support
# Based on official nginx:alpine with Brotli module compiled in

FROM nginx:alpine AS builder

# Install build dependencies
RUN apk add --no-cache \
    gcc \
    g++ \
    make \
    cmake \
    pcre-dev \
    zlib-dev \
    linux-headers \
    curl \
    git

# Get nginx version and download matching source
RUN nginx_version=$(nginx -v 2>&1 | sed 's/.*nginx\///' | sed 's/ .*//') && \
    echo "Building for nginx version: $nginx_version" && \
    curl -fSL https://nginx.org/download/nginx-${nginx_version}.tar.gz -o nginx.tar.gz && \
    tar -xzf nginx.tar.gz && \
    mv nginx-${nginx_version} /nginx

# Clone and build Brotli module
RUN git clone --recurse-submodules https://github.com/google/ngx_brotli.git /ngx_brotli && \
    cd /ngx_brotli && \
    git submodule update --init --recursive

# Build Brotli libraries first
RUN cd /ngx_brotli/deps/brotli && \
    mkdir out && \
    cd out && \
    cmake -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF .. && \
    make && \
    make install

# Configure and build dynamic modules
RUN cd /nginx && \
    ./configure --with-compat \
        --add-dynamic-module=/ngx_brotli \
        --prefix=/etc/nginx \
        --sbin-path=/usr/sbin/nginx \
        --modules-path=/usr/lib/nginx/modules \
        --conf-path=/etc/nginx/nginx.conf \
        --error-log-path=/var/log/nginx/error.log \
        --http-log-path=/var/log/nginx/access.log \
        --pid-path=/var/run/nginx.pid \
        --lock-path=/var/run/nginx.lock && \
    make modules

# Production stage - use official nginx:alpine
FROM nginx:alpine

# Copy compiled Brotli modules from builder
COPY --from=builder /nginx/objs/ngx_http_brotli_filter_module.so /usr/lib/nginx/modules/
COPY --from=builder /nginx/objs/ngx_http_brotli_static_module.so /usr/lib/nginx/modules/

# Copy our site configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Create proper nginx.conf with Brotli directives
RUN cat > /etc/nginx/nginx.conf << 'EOF'
load_module modules/ngx_http_brotli_filter_module.so;
load_module modules/ngx_http_brotli_static_module.so;

user  nginx;
worker_processes  auto;

error_log  /var/log/nginx/error.log notice;
pid        /var/run/nginx.pid;

events {
    worker_connections  1024;
}

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;

    log_format  main  '$remote_addr - $remote_user [$time_local] "$request" '
                      '$status $body_bytes_sent "$http_referer" '
                      '"$http_user_agent" "$http_x_forwarded_for"';

    access_log  /var/log/nginx/access.log  main;

    sendfile        on;
    tcp_nopush      on;
    keepalive_timeout  65;

    # Brotli Settings
    brotli on;
    brotli_static on;
    brotli_comp_level 5;
    brotli_types text/plain text/css application/javascript application/json application/xml image/svg+xml;

    include /etc/nginx/conf.d/*.conf;
}
EOF

EXPOSE 80 443

CMD ["nginx", "-g", "daemon off;"]