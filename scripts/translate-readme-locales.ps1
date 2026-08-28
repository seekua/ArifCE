param(
  [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$locales = [ordered]@{
  'ar'='AR'; 'da'='DA'; 'de'='DE'; 'el'='EL'; 'es'='ES'; 'fr'='FR'; 'it'='IT'; 'ja'='JA';
  'ko'='KO'; 'no'='NB'; 'pl'='PL'; 'pt-BR'='PT-BR'; 'ru'='RU'; 'th'='TH'; 'uk'='UK';
  'zh-CN'='ZH'; 'zh-TW'='ZH'
}
$source = Get-Content (Join-Path $Root 'README.md')

function Invoke-DeepL([string]$text, [string]$target) {
  if ([string]::IsNullOrWhiteSpace($text)) { return $text }
  $protected = @{}
  $i = 0
  $safe = [regex]::Replace($text, '(?<x>`[^`]+`|https?://[^\s)>]+|<[^>]+>|\([^)]*://[^)]*\))', {
    param($m)
    $key = "ZXQPLACEHOLDER$($i)ZXQ"; $protected[$key] = $m.Value; $i++; $key
  })
  $job = @{ kind='default'; raw_en_sentence=$safe }
  $params = @{ lang=@{source_lang='EN';target_lang=$target}; jobs=@($job); priority=-1; timestamp=[int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()) }
  $body = @{jsonrpc='2.0';method='LMT_handle_jobs';id=1;params=$params} | ConvertTo-Json -Depth 10
  for ($attempt=0; $attempt -lt 5; $attempt++) {
    try { $result = Invoke-RestMethod -Uri 'https://www2.deepl.com/jsonrpc' -Method Post -ContentType 'application/json' -Body $body; break }
    catch { if ($attempt -eq 4) { throw }; Start-Sleep -Seconds (2 * ($attempt + 1)) }
  }
  $out = $result.result.translations[0].beams[0].postprocessed_sentence
  foreach ($key in $protected.Keys) { $out = $out.Replace($key, $protected[$key]) }
  return $out
}

foreach ($locale in $locales.Keys) {
  $target = $locales[$locale]
  $out = [System.Collections.Generic.List[string]]::new()
  $inFence = $false
  foreach ($line in $source) {
    if ($line.TrimStart().StartsWith('```')) { $inFence = -not $inFence; $out.Add($line); continue }
    if ($inFence -or [string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*<.*>\s*$' -or $line -match '^\s*!\[' -or $line -match '^\s*\[[^]]+\]\(README\.' -or $line -match '^\s*\[[^]]+\]\(https?://') { $out.Add($line); continue }
    try { $out.Add((Invoke-DeepL $line $target)) } catch { Write-Warning "$locale failed: $($_.Exception.Message)"; $out.Add($line) }
  }
  $path = Join-Path $Root "README.$locale.md"
  Set-Content -Path $path -Value $out -Encoding utf8
  Write-Host "translated $locale"
}
