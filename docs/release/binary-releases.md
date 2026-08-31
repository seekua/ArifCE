# Self-contained binary releases

Tagged releases prepare downloadable CLI archives for Windows, Linux, and macOS through `.github/workflows/release-binaries.yml`.

Each archive contains the self-contained executable and `SHA256SUMS`. Download the three artifacts from the completed Actions run and attach them to the corresponding GitHub Release. The workflow intentionally has read-only repository permissions; release publication and overwrite remain a deliberate maintainer action.

The .NET global tool remains the supported fallback. Release signing, additional architectures, package-manager manifests, and NativeAOT remain separate follow-up work.
