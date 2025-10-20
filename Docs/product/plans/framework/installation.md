# Framework Installation

Guidance for packaging and distributing the Webstir CLI so developers can install it with modern tooling while keeping workspace dependencies in sync.

## Goals
- Offer one-command installs on every major platform.
- Publish signed, reproducible binaries that map back to each release.
- Keep downstream installers in lockstep by sourcing metadata from a single manifest.
- Preserve the offline-friendly workflow inside projects by continuing to run `webstir install` after scaffolding.

## Release Pipeline
1. Build platform-specific, self-contained binaries via `publish.sh` (macOS arm64/x64, Linux x64/arm64, Windows x64).
2. Generate a release manifest (`install-manifest.json`) that records version, platform identifiers, download URLs, and checksums for each binary.
3. Attach binaries and the manifest to the GitHub release (or artifact store) and sign them with `cosign` or `minisign` for verification.
4. Notify downstream installers (Homebrew tap, npm wrapper, curl script, winget/Scoop specs) to consume the manifest so every channel stays current automatically.

## Distribution Channels

### Homebrew (macOS & Linux)
- Maintain a tap such as `electric/webstir` with a formula that downloads the matching binary from the manifest URL and validates its `sha256`.
- Provide separate bottles for `arm64` and `x86_64`, and add `depends_on "node"` if Node is not bundled.
- Include a `test do` block that runs `webstir --version` to satisfy Homebrew audit requirements.
- Once stable, upstream to `homebrew-core` for automatic updates when the tap formula is bumped.

### npm Wrapper
- Publish `@electric-coding/webstir` with a lightweight JavaScript launcher.
- During `postinstall`, read the manifest, download the correct binary for the current `os/arch`, verify the checksum, and cache it under `~/.webstir` (or npm cache) for reuse.
- Expose the CLI through the package `bin` field so `npm install -g @electric-coding/webstir` and `npx webstir` work out of the box.
- Fail gracefully offline by reusing cached binaries; surface clear instructions to fetch updates once connectivity is restored.

### Curl-able Installer
- Provide `https://example.com/install.sh` that fetches the manifest, chooses the right binary, verifies signature + checksum, and installs to `~/.local/bin` (or prompts for `/usr/local/bin`).
- Support `--version` and `--install-dir` flags so CI pipelines and container images can pin specific releases.
- Publish a companion PowerShell script for Windows users who prefer manual installs.

### Windows Package Managers
- Generate winget manifests via `wingetcreate` pointing at the Windows binary URL and checksum.
- Maintain a Scoop bucket entry that downloads and verifies the binary, placing shims in place.
- Keep both manifests in sync with the release manifest to avoid manual drift.

### Linux Packages (Optional)
- Optionally build `.deb` and `.rpm` packages that install the binary to `/usr/bin` and drop a man page.
- Host them on packagecloud or GitHub Packages, and link the metadata from the release manifest for consistency.

### Version Managers
- Ship an `asdf` (and optionally `mise`) plugin that reads the release manifest to install requested versions.
- Provide a simple `bin/install` script that delegates to the curl installer, preserving checksum validation.

## Post-Install Workflow
Regardless of how the CLI is installed:
- Encourage users to run `webstir install` inside each workspace to synchronize the pinned framework packages from the registry (`--dry-run` to audit, `--clean` to refresh caches). Mention that GitHub Packages credentials (`GH_PACKAGES_TOKEN`) are required until the packages move to npm.
- Remind users that the CLI remains self-contained; no additional .NET runtime is required after installation.

## Maintainer Checklist
- [ ] Run `publish.sh` for every supported platform.
- [ ] Update `install-manifest.json` with new version numbers, URLs, and checksums.
- [ ] Sign artifacts and update the public keys used by installers.
- [ ] Bump Homebrew tap formula, winget/Scoop manifests, and npm wrapper to reference the new release.
- [ ] Regenerate installer scripts if defaults changed (paths, flags, dependencies).
- [ ] Verify each channel on a clean machine or container (Homebrew install, npm global install, curl script, winget, Scoop).
- [ ] Update docs or release notes with any behavioral changes.
