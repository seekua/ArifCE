[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-graph-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-graph-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Anchor([string]$Text, [string]$Before, [string]$After, [int]$Count = 1) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne $Count) { throw 'Unexpected graph mutation anchor count; refusing uncalibrated control.' }
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
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkGraphTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkGraphSource ([IO.File]::ReadAllText($testPath))))
    $graphPath = Join-Path $checkout 'src/ArifCE.Infrastructure/CodeGraph.cs'
    $original = [IO.File]::ReadAllText($graphPath).Replace("`r`n", "`n")
    $methods = @('Graph_preserves_declarations_and_relationship_confidence', 'Graph_tracks_source_lifecycle_and_ignores_derived_noise', 'Graph_rebuild_is_equivalent_and_preserves_canonical_bytes', 'Graph_trusted_closure_excludes_heuristics_and_follows_project_dependents')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Graph calibration restore failed; see $root" }
        foreach ($variant in @('good', 'promote-heuristics', 'stale-cache', 'drop-calls', 'collapse-overloads', 'reverse-dependents', 'rewrite-canonical')) {
            $source = $original
            switch ($variant) {
                'promote-heuristics' { $source = Replace-Anchor $source '"HEURISTIC"' '"EXACT"' 2 }
                'stale-cache' { $source = Replace-Anchor $source 'return string.Equals(graph.SourceDigest, currentDigest, StringComparison.Ordinal) ? graph : await BuildAsync(root, cancellationToken);' '_ = currentDigest; return graph;' }
                'drop-calls' { $source = Replace-Anchor $source 'edges.Add(new CodeGraphEdge(sourceId, target.Id, "CALLS", "HEURISTIC"));' '_ = sourceId;' }
                'collapse-overloads' { $source = Replace-Anchor $source 'nodes.DistinctBy(node => node.Id)' 'nodes.DistinctBy(node => (node.Path, node.Name, node.Kind))' }
                'reverse-dependents' { $source = Replace-Anchor $source '{ Confidence: "EXACT", Kind: "PROJECT_REFERENCE" } when edge.To == id => edge.From,' '{ Confidence: "EXACT", Kind: "PROJECT_REFERENCE" } when edge.From == id => edge.To,' }
                'rewrite-canonical' { $source = Replace-Anchor $source 'var path = GraphPath(fullRoot);' 'await File.WriteAllTextAsync(Path.Combine(fullRoot, ".arifce", "CURRENT.md"), "Rewritten by derived graph", cancellationToken); var path = GraphPath(fullRoot);' }
            }
            [IO.File]::WriteAllText($graphPath, $source)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Graph calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Graph calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Graph calibration passed: good code and six incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-graph-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe graph calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
