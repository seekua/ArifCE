# Installation

ArifCE V0.8.0 ships as self-contained archives for Windows x64, Linux x64/ARM64, and macOS Intel/Apple Silicon. Git is required; the .NET SDK is required only when building from source.

1. Download the matching `arifce-<runtime>.zip` from the [V0.8.0 release](https://github.com/seekua/ArifCE/releases/tag/v0.8.0).
2. Verify the archive against the release-level `SHA256SUMS` file.
3. Extract the archive and place `arifce` (or `arifce.exe` on Windows) on your `PATH`.
4. Run `arifce help`.

Maintainers can reproduce a self-contained binary locally:

```powershell
./scripts/publish-self-contained.ps1 -Runtime win-x64
```

The five-platform self-contained matrix is verified in remote CI for Windows x64, Linux x64/ARM64, and macOS Intel/Apple Silicon. NativeAOT remains a separate, currently blocked compatibility track; see the [distribution plan](../release/native-aot-distribution.md).

## Run from source

```bash
dotnet restore ArifCE.slnx
dotnet build ArifCE.slnx --configuration Release --no-restore
dotnet run --project src/ArifCE.Cli -- help
```

## Install the local global-tool package

```bash
dotnet pack src/ArifCE.Cli/ArifCE.Cli.csproj --configuration Release --output ./artifacts/packages
dotnet tool install --global ArifCE.Cli --version 0.8.0 --add-source ./artifacts/packages
arifce help
```

Use `dotnet tool update` instead of `install` when the same tool ID is already installed. The repeatable package verification used by this repository is:

```powershell
./scripts/package-smoke.ps1 -Configuration Release
```

The smoke script installs into a temporary tool path and deletes that temporary environment after validation. It does not modify the user's global tool installation.

## Platform status

Remote CI verifies the source build/package flow on Windows, Ubuntu, and macOS and executes each self-contained binary on a runner with the same architecture. See [binary release evidence](../release/binary-releases.md).
