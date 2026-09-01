# Installation

ArifCE V0.7.0 requires the .NET 10 SDK and Git. The published NuGet tool is available from the GitHub release.

For a machine that should not install the .NET runtime separately, maintainers can build a self-contained CLI binary for a supported runtime:

```powershell
./scripts/publish-self-contained.ps1 -Runtime win-x64
```

The five-platform self-contained matrix is verified in remote CI for Windows x64, Linux x64/ARM64, and macOS Intel/Apple Silicon. Until a new version tag publishes the corresponding immutable archives, these builds remain CI artifacts rather than advertised `v0.7.0` release assets. NativeAOT remains a separate, currently blocked compatibility track; see the [distribution plan](../release/native-aot-distribution.md).

## Run from source

```bash
dotnet restore ArifCE.slnx
dotnet build ArifCE.slnx --configuration Release --no-restore
dotnet run --project src/ArifCE.Cli -- help
```

## Install the local global-tool package

```bash
dotnet pack src/ArifCE.Cli/ArifCE.Cli.csproj --configuration Release --output ./artifacts/packages
dotnet tool install --global ArifCE.Cli --version 0.7.0
arifce help
```

Use `dotnet tool update` instead of `install` when the same tool ID is already installed. The repeatable package verification used by this repository is:

```powershell
./scripts/package-smoke.ps1 -Configuration Release
```

The smoke script installs into a temporary tool path and deletes that temporary environment after validation. It does not modify the user's global tool installation.

## Platform status

The current local evidence is Windows/net10.0. CI is configured for Windows, Ubuntu, and macOS, but remote results must exist before those platforms are marked verified.
