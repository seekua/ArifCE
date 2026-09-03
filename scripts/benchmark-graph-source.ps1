function ConvertTo-BenchmarkGraphSource([string]$Source) {
    foreach ($marker in @('namespace ArifCE.Tests;', 'public sealed class BenchmarkGraphTests')) {
        if ([regex]::Matches($Source, [regex]::Escape($marker)).Count -ne 1) { throw 'Unexpected pinned graph evaluator source shape.' }
    }
    return $Source.Replace('namespace ArifCE.Tests;', 'namespace ArifCE.IndependentEvaluator;').Replace('public sealed class BenchmarkGraphTests', 'public sealed class IndependentTests')
}
