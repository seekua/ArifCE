[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-verification-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-verification-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Anchor([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Unexpected verification mutation anchor count; refusing uncalibrated control.' }
    return $Text.Replace($Before, $After)
}
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $commit = (& git -C $repo rev-parse "$SourceCommit^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Cannot resolve calibration source.' }
    $archive = Join-Path $root 'source.zip'; & git -C $repo archive --format=zip "--output=$archive" $commit
    if ($LASTEXITCODE -ne 0) { throw 'Unable to export calibration source.' }
    $checkout = Join-Path $root 'checkout'; Expand-Archive -LiteralPath $archive -DestinationPath $checkout
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkVerificationTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkVerificationSource ([IO.File]::ReadAllText($testPath))))
    $parserPath = Join-Path $checkout 'src/ArifCE.Infrastructure/CommandEvidenceParser.cs'
    $servicePath = Join-Path $checkout 'src/ArifCE.Infrastructure/ProjectService.cs'
    $parserOriginal = [IO.File]::ReadAllText($parserPath).Replace("`r`n", "`n"); $serviceOriginal = [IO.File]::ReadAllText($servicePath).Replace("`r`n", "`n")
    $methods = @('Named_help_command_never_becomes_verified_test_evidence', 'Unsafe_or_secret_commands_cannot_create_verified_or_partial_canonical_state')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log'); if ($LASTEXITCODE -ne 0) { throw "Verification calibration restore failed; see $root" }
        foreach ($variant in @('good', 'help-is-test', 'unsafe-is-verified', 'secret-command-executes', 'unsafe-needs-no-approval')) {
            $parser = $parserOriginal; $service = $serviceOriginal
            switch ($variant) {
                'help-is-test' { if ([regex]::Matches($parser, [regex]::Escape(': ("COMMAND", null);')).Count -ne 2) { throw 'Unexpected parser mutation anchor count.' }; $parser = $parser.Replace(': ("COMMAND", null);', ': ("TEST_RUN", null);') }
                'unsafe-is-verified' { $service = Replace-Anchor $service 'policy == VerificationCommandKind.UnsafeShell || parsed.Kind == "COMMAND" ? ClaimStatus.Supported' 'parsed.Kind == "COMMAND" ? ClaimStatus.Supported' }
                'secret-command-executes' { $service = Replace-Anchor $service 'if (commandRedaction.Count > 0) throw new InvalidOperationException("Verification command contains a detectable secret and was blocked before execution.");' '_ = commandRedaction.Count;' }
                'unsafe-needs-no-approval' { $service = Replace-Anchor $service 'if (policy == VerificationCommandKind.UnsafeShell && !allowUnsafeCommand) throw new InvalidOperationException("Unrecognized verification commands require explicit --allow-unsafe-command approval.");' '_ = allowUnsafeCommand;' }
            }
            [IO.File]::WriteAllText($parserPath, $parser); [IO.File]::WriteAllText($servicePath, $service)
            $results = Join-Path $root $variant; New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods; $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Verification calibration $variant expected $expected, got $($assessment.status). Logs: $results" }; Write-Output "Verification calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true; Write-Output "Verification calibration passed: good code and four incorrect variants at $commit. Not a model benchmark."
}
finally { $resolved = [IO.Path]::GetFullPath($root); if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-verification-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe verification calibration cleanup path.' }; if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force } }
exit 0
