# Self-contained binary releases

Tagged releases build downloadable, self-contained CLI archives through `.github/workflows/release-binaries.yml` for:

- Windows x64 (`win-x64`)
- Linux x64 (`linux-x64`)
- Linux ARM64 (`linux-arm64`)
- macOS Intel (`osx-x64`)
- macOS Apple Silicon (`osx-arm64`)

Each matrix job runs the produced executable on a runner with the same architecture, then packages it with a checksum for the executable. The bundle job verifies all five archives, creates a release-level `SHA256SUMS`, and publishes one verified Actions artifact containing the six files. The workflow intentionally retains read-only repository permissions; publishing or replacing GitHub Release assets remains an explicit maintainer action.

Before attaching an archive, verify its integrity locally:

```powershell
./scripts/verify-release-artifacts.ps1 -Archive ./arifce-win-x64.zip
```

The .NET global tool remains the supported fallback. Release signing, additional architectures, package-manager manifests, and NativeAOT remain separate follow-up work.
