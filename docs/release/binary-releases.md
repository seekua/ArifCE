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

A local .NET global-tool package built from source remains a development fallback. ArifCE is not currently claimed as published on NuGet.org. Release signing, additional architectures, package-manager manifests, and NativeAOT remain separate follow-up work.

## Verification evidence

Remote CI run [33490587568](https://github.com/seekua/ArifCE/actions/runs/33490587568) passed at commit `d26131f` on 2026-09-01. It completed the three-OS build/test/package matrix and all five native self-contained smoke jobs. The release-bundle script was also exercised locally with five archive fixtures, including archive-internal and release-level checksum validation.

The complete publishing path was exercised by annotated tag [`v0.8.0`](https://github.com/seekua/ArifCE/tree/v0.8.0) at commit [`af1711c`](https://github.com/seekua/ArifCE/commit/af1711ca337168f1e2103f6cbfbd42e9c433197b). [Tag CI](https://github.com/seekua/ArifCE/actions/runs/33496418909) and the [native release workflow](https://github.com/seekua/ArifCE/actions/runs/33496418947) passed. The resulting [public release](https://github.com/seekua/ArifCE/releases/tag/v0.8.0) contains exactly the five platform archives and `SHA256SUMS`; all assets were downloaded again and verified against both release-level and archive-internal checksums. A manual workflow run still requires an existing tag whose semantic version exactly matches the CLI package version.
