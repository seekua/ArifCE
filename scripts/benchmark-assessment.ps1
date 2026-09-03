function Read-BenchmarkAssessment([string]$TrxPath, [int]$ExitCode, [string[]]$Methods) {
    $errorResult = { param($reason) [pscustomobject]@{ status='ERROR'; taskPassed=$null; reason=$reason } }
    if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) { return (& $errorResult 'No test result file; compilation, restore, or runner failure is not a scored assertion.') }
    $reader = $null
    try {
        $settings = [Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create($TrxPath, $settings)
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        $results = @($document.SelectNodes("//*[local-name()='UnitTestResult']"))
    } catch { return (& $errorResult 'Malformed test result file.') }
    finally { if ($null -ne $reader) { $reader.Dispose() } }
    if ($results.Count -ne $Methods.Count -or $Methods.Count -eq 0) { return (& $errorResult 'Expected evaluator tests did not all run exactly once.') }
    foreach ($method in $Methods) {
        $matching = @($results | Where-Object { $_.testName -ceq "ArifCE.IndependentEvaluator.IndependentTests.$method" })
        if ($matching.Count -ne 1 -or $matching[0].outcome -cnotin @('Passed','Failed')) { return (& $errorResult 'Missing, duplicate, skipped, or unknown evaluator test outcome.') }
    }
    $failed = @($results | Where-Object outcome -ceq 'Failed').Count -gt 0
    if (($ExitCode -eq 0) -eq $failed) { return (& $errorResult 'Process exit and test assertions disagree.') }
    return [pscustomobject]@{ status=$(if ($failed) { 'FAILED' } else { 'PASSED' }); taskPassed=(-not $failed); reason='Executed pinned test assertions only; not complete product correctness.' }
}
