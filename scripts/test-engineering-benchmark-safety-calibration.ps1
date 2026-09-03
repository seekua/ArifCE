[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-safety-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-safety-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Once([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Calibration mutation anchor is not unique; refusing an uncalibrated control.' }
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
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkSafetyTests.cs'
    $testSource = ConvertTo-BenchmarkSafetySource ([IO.File]::ReadAllText($testPath))
    [IO.File]::WriteAllText($testPath, $testSource)
    $llmPath = Join-Path $checkout 'src/ArifCE.Infrastructure/LlmOrchestration.cs'
    $servicePath = Join-Path $checkout 'src/ArifCE.Infrastructure/ProjectService.cs'
    $llmOriginal = [IO.File]::ReadAllText($llmPath).Replace("`r`n", "`n")
    $serviceOriginal = [IO.File]::ReadAllText($servicePath).Replace("`r`n", "`n")
    $secretMethod = 'Secret_boundary_checks_provider_calls_and_persisted_response'
    $acceptanceMethod = 'High_risk_acceptance_checks_each_requirement_and_success'
    $methods = @($secretMethod, $acceptanceMethod)
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    $guard = '        if (outbound.Count > 0)' + "`n" + '            throw new InvalidOperationException("LLM execution blocked: the prompt contains a detectable secret. Remove or redact it before sending to a provider.");' + "`n"
    $variants = @('good', 'reject-all-llm', 'late-secret-check', 'unsafe-response', 'reject-all-acceptance', 'skip-build', 'skip-tests', 'skip-review')
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Calibration restore failed; see $root" }
        foreach ($variant in $variants) {
            $llm = $llmOriginal; $service = $serviceOriginal
            switch ($variant) {
                'reject-all-llm' { $llm = Replace-Once $llm '        var redactor = new SecretRedactor();' ('        if (request.Prompt.Length >= 0) throw new InvalidOperationException("calibration reject all");' + "`n" + '        var redactor = new SecretRedactor();') }
                'late-secret-check' {
                    $llm = Replace-Once $llm $guard ''
                    $route = '        var route = await _router.CompleteAsync(request, preferred, cancellationToken);'
                    $llm = Replace-Once $llm $route ($route + "`n" + $guard)
                }
                'unsafe-response' { $llm = Replace-Once $llm 'var safeResponse = route.Response with { Text = response.Text, RawResponse = string.Empty };' 'var safeResponse = route.Response;' }
                'reject-all-acceptance' { $service = Replace-Once $service '        if (string.IsNullOrWhiteSpace(actor))' ('        if (actor.Length >= 0) throw new InvalidOperationException("calibration reject all");' + "`n" + '        if (string.IsNullOrWhiteSpace(actor))') }
                'skip-build' { $service = Replace-Once $service 'if (requirements.Build &&' 'if (claim.Id.Length < 0 && requirements.Build &&' }
                'skip-tests' { $service = Replace-Once $service 'if (requirements.Tests &&' 'if (claim.Id.Length < 0 && requirements.Tests &&' }
                'skip-review' { $service = Replace-Once $service 'if (requirements.IndependentReview)' 'if (claim.Id.Length < 0 && requirements.IndependentReview)' }
            }
            [IO.File]::WriteAllText($llmPath, $llm)
            [IO.File]::WriteAllText($servicePath, $service)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $exit = $LASTEXITCODE
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $exit $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Safety evaluator calibration passed: known-good and seven incorrect implementations at $commit. Synthetic controls, not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-safety-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe calibration cleanup path.' }
    # Keep failed fixtures for diagnosis; remove only after the complete calibration succeeded.
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
