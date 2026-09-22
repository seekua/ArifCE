function Get-BenchmarkOperationLockName([string]$Root) {
    $trimCharacters = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $normalized = [IO.Path]::GetFullPath($Root).TrimEnd($trimCharacters).ToUpperInvariant()
    $bytes = [Text.Encoding]::UTF8.GetBytes($normalized)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return 'ArifCEBenchmarkOperation-' + [Convert]::ToHexString($digest).Substring(0, 32)
}

function Invoke-WithBenchmarkOperationLock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][scriptblock]$Operation,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 600
    )

    $mutex = [Threading.Mutex]::new($false, (Get-BenchmarkOperationLockName $Root))
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne($TimeoutSeconds * 1000) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw "Timed out waiting for the benchmark build/test operation lock after $TimeoutSeconds seconds." }
        & $Operation
    }
    finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}
