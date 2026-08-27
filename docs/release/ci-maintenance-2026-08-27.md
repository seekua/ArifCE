# CI Maintenance Evidence — 2026-08-27

GitHub Actions workflow actions were upgraded from `actions/checkout@v4` and `actions/setup-dotnet@v4` to their Node 24-based `v5` majors.

GitHub Actions run [33060123591](https://github.com/seekua/ArifCE/actions/runs/33060123591) completed successfully for commit `b810ec4cb966dc07ddc18267bea7f69f22e27ee0`:

| Runner | Result | Duration |
|---|---|---:|
| `windows-latest` | success | 2 minutes 22 seconds |
| `ubuntu-latest` | success | 1 minute 2 seconds |
| `macos-latest` | success | 1 minute 10 seconds |

Every job completed checkout, .NET setup, restore, Release build, tests, secret scan, and the packaged-tool smoke fixture. The prior Node 20 deprecation annotations were absent from this run.

This evidence closes `FINDING-0002`; it does not alter the published `v0.1.0` release asset.
