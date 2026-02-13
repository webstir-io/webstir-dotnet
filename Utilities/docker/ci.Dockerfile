FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim

# Install Node.js 20.x
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg git procps build-essential python3 \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs libvips libvips-dev pkg-config \
    && npm install -g typescript \
    && rm -rf /var/lib/apt/lists/*

ENV BUN_INSTALL=/root/.bun
ENV PATH=$BUN_INSTALL/bin:$PATH

# Install Bun 1.3.5 to mirror GitHub Actions
RUN curl -fsSL https://bun.sh/install | bash -s -- bun-v1.3.5

# Show tool versions in the build log for quick diagnostics
RUN dotnet --info && node -v && npm -v && bun -v

WORKDIR /workspace

# Default command mirrors CI steps; can be overridden at runtime
CMD ["bash", "-lc", "./utilities/scripts/local-ci.sh"]
