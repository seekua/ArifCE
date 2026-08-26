# Installation

ArifCE V0.1 requires the .NET 10 SDK and Git. Published NuGet distribution is not claimed yet; install from a local source checkout.

## Run from source

```bash
dotnet restore ArifCE.slnx
dotnet build ArifCE.slnx --configuration Release --no-restore
dotnet run --project src/ArifCE.Cli -- help
```

## Install the local global-tool package

```bash
dotnet pack src/ArifCE.Cli/ArifCE.Cli.csproj --configuration Release --output ./artifacts/packages
dotnet tool install --global --add-source ./artifacts/packages ArifCE.Cli --version 0.1.0
arifce help
```

Use `dotnet tool update` instead of `install` when the same tool ID is already installed. The repeatable package verification used by this repository is:

```powershell
./scripts/package-smoke.ps1 -Configuration Release
```

The smoke script installs into a temporary tool path and deletes that temporary environment after validation. It does not modify the user's global tool installation.

## Platform status

The current local evidence is Windows/net10.0. CI is configured for Windows, Ubuntu, and macOS, but remote results must exist before those platforms are marked verified.
