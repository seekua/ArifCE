function ConvertTo-BenchmarkStorageSource([string]$Source) {
    foreach ($marker in @('namespace ArifCE.Tests;', 'public sealed class BenchmarkStorageTests')) {
        if ([regex]::Matches($Source, [regex]::Escape($marker)).Count -ne 1) { throw 'Unexpected pinned storage evaluator source shape.' }
    }
    # Keep worker entrypoint/barriers in the pinned source, never in candidate-authored helpers.
    return $Source.Replace('namespace ArifCE.Tests;', 'namespace ArifCE.IndependentEvaluator;').Replace('public sealed class BenchmarkStorageTests', 'public sealed class IndependentTests')
}
