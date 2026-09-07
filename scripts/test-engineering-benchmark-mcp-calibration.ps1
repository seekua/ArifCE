[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-mcp-boundary-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-mcp-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Anchor([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Unexpected MCP mutation anchor count; refusing uncalibrated control.' }
    return $Text.Replace($Before, $After)
}
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $commit = (& git -C $repo rev-parse "$SourceCommit^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Cannot resolve calibration source.' }
    $archive = Join-Path $root 'source.zip'
    & git -C $repo archive --format=zip "--output=$archive" $commit
    if ($LASTEXITCODE -ne 0) { throw 'Unable to export calibration source.' }
    $checkout = Join-Path $root 'checkout'
    Expand-Archive -LiteralPath $archive -DestinationPath $checkout
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkMcpBoundaryTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkMcpBoundarySource ([IO.File]::ReadAllText($testPath))))
    $mcpPath = Join-Path $checkout 'src/ArifCE.Mcp/Program.cs'
    $original = [IO.File]::ReadAllText($mcpPath).Replace("`r`n", "`n")
    $methods = @('Mcp_rejects_malformed_writes_before_canonical_side_effects', 'Mcp_rejects_oversize_and_duplicate_writes_without_creating_extra_records', 'Mcp_valid_writes_follow_canonical_domain_rules')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "MCP calibration restore failed; see $root" }
        foreach ($variant in @('good', 'invalid-risk-falls-back', 'allow-path-like-id', 'allow-unknown-arguments', 'allow-wrong-required-type', 'remove-request-limit')) {
            $mcp = $original
            switch ($variant) {
                'invalid-risk-falls-back' { $mcp = Replace-Anchor $mcp 'return System.Enum.TryParse<T>(text, true, out var parsed) && System.Enum.IsDefined(parsed) ? parsed : throw new McpException(-32602, $"Invalid {name} value: {text}");' 'return System.Enum.TryParse<T>(text, true, out var parsed) && System.Enum.IsDefined(parsed) ? parsed : fallback;' }
                'allow-path-like-id' { $mcp = Replace-Anchor $mcp '&& !SafeId().IsMatch(property.Value.GetString() ?? string.Empty)' '&& false' }
                'allow-unknown-arguments' { $mcp = Replace-Anchor $mcp 'foreach (var property in arguments.EnumerateObject()) if (!allowedSet.Contains(property.Name)) throw new McpException(-32602, $"Unknown argument for {tool}: {property.Name}");' '_ = allowedSet;' }
                'allow-wrong-required-type' { $mcp = Replace-Anchor $mcp 'if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString())) throw new McpException(-32602, $"Missing required argument: {name}");' 'if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var property)) throw new McpException(-32602, $"Missing required argument: {name}");' }
                'remove-request-limit' { $mcp = Replace-Anchor $mcp 'if (line.Length > MaxRequestCharacters) throw new McpException(-32600, $"Request exceeds the {MaxRequestCharacters}-character limit.");' '_ = line.Length;' }
            }
            [IO.File]::WriteAllText($mcpPath, $mcp)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "MCP calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "MCP calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "MCP calibration passed: good code and five incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-mcp-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe MCP calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
