# Self-contained binary releases

Tagged releases build downloadable, self-contained CLI archives through `.github/workflows/release-binaries.yml` for:

- Windows x64 (`win-x64`)
- Linux x64 (`linux-x64`)
- Linux ARM64 (`linux-arm64`)
- macOS Intel (`osx-x64`)
- macOS Apple Silicon (`osx-arm64`)

Each matrix job runs the produced executable on a runner with the same architecture, then packages it with a checksum for the executable. The bundle job verifies all five archives, creates a release-level `SHA256SUMS`, and publishes one verified Actions artifact containing the six files. Only the final bundle job receives `contents: write`; it can create a release for the exact version tag and upload the verified assets. It checks every asset name first and fails closed instead of replacing an existing asset.

Before attaching an archive, verify its integrity locally:

```powershell
./scripts/verify-release-artifacts.ps1 -Archive ./arifce-win-x64.zip
```

The .NET global tool remains the supported fallback. Release signing, additional architectures, package-manager manifests, and NativeAOT remain separate follow-up work.

## Verification evidence

Remote CI run [#319](https://github.com/seekua/ArifCE/actions/runs/33490587568) passed at commit `d26131f` on 2026-09-01. It completed the three-OS build/test/package matrix and all five native self-contained smoke jobs. The release-bundle script was also exercised locally with five archive fixtures, including archive-internal and release-level checksum validation.

The revised publishing path has not yet been exercised by a new version tag. Neither the existing `v0.7.0` tag nor its source archive is rewritten to contain binaries built from later commits. A manual workflow run also requires an existing tag whose semantic version exactly matches the CLI package version.
