function ConvertTo-BenchmarkContractSource([string]$Source) {
    foreach ($marker in @('namespace ArifCE.Tests;', 'public sealed class BenchmarkContractTests')) {
        if ([regex]::Matches($Source, [regex]::Escape($marker)).Count -ne 1) { throw 'Unexpected pinned contract evaluator source shape.' }
    }
    return $Source.Replace('namespace ArifCE.Tests;', 'namespace ArifCE.IndependentEvaluator;').Replace('public sealed class BenchmarkContractTests', 'public sealed class IndependentTests')
}
