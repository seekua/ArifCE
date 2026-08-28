param([string]$Root = (Split-Path -Parent $PSScriptRoot))

# DeepL web targets. Bengali, Bosnian and Thai are unsupported by this endpoint.
$locales = [ordered]@{
  'ar'='AR'; 'da'='DA'; 'de'='DE'; 'el'='EL'; 'es'='ES'; 'fr'='FR'; 'it'='IT'; 'ja'='JA';
  'ko'='KO'; 'no'='NB'; 'pl'='PL'; 'pt-BR'='PT-BR'; 'ru'='RU'; 'uk'='UK'; 'vi'='VI';
  'zh-CN'='ZH'; 'zh-TW'='ZH'
}
$source = Get-Content (Join-Path $Root 'README.md')

function Protect([string]$text) {
  $map=@{}; $n=0
  $safe=[regex]::Replace($text,'(?<x>`[^`]+`|https?://[^\s)>]+|<[^>]+>|\([^)]*://[^)]*\))',{param($m) $key="ZXQPLACEHOLDER$($n)ZXQ";$map[$key]=$m.Value;$n++;$key})
  [pscustomobject]@{Text=$safe;Map=$map}
}
function TranslateBatch([string[]]$texts,[string]$target) {
  $prepared=@($texts|ForEach-Object{Protect $_});$jobs=@($prepared|ForEach-Object{@{kind='default';raw_en_sentence=$_.Text}})
  $params=@{lang=@{source_lang='EN';target_lang=$target};jobs=$jobs;priority=-1;timestamp=[int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())}
  $body=@{jsonrpc='2.0';method='LMT_handle_jobs';id=1;params=$params}|ConvertTo-Json -Depth 12
  for($attempt=0;$attempt -lt 6;$attempt++){try{$result=Invoke-RestMethod -Uri 'https://www2.deepl.com/jsonrpc' -Method Post -ContentType 'application/json' -Body $body;break}catch{if($attempt -eq 5){throw};Start-Sleep -Seconds (3*($attempt+1))}}
  $translated=@($result.result.translations|ForEach-Object{$_.beams[0].postprocessed_sentence})
  for($i=0;$i -lt $translated.Count;$i++){$v=$translated[$i];foreach($key in $prepared[$i].Map.Keys){$v=$v.Replace($key,$prepared[$i].Map[$key])};$translated[$i]=$v};,$translated
}

foreach($locale in $locales.Keys){
  $target=$locales[$locale];$out=[System.Collections.Generic.List[string]]::new();$batch=[System.Collections.Generic.List[string]]::new();$idx=[System.Collections.Generic.List[int]]::new();$inFence=$false
  function FlushBatch{if($batch.Count -eq 0){return};try{$translated=TranslateBatch $batch.ToArray() $target;for($j=0;$j -lt $idx.Count;$j++){$out[$idx[$j]]=$translated[$j]}}catch{Write-Warning "$locale batch failed: $($_.Exception.Message)"};$batch.Clear();$idx.Clear();Start-Sleep -Milliseconds 800}
  foreach($line in $source){if($line.TrimStart().StartsWith('```')){$inFence=-not $inFence;$out.Add($line);continue};$skip=$inFence -or [string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*<.*>\s*$' -or $line -match '^\s*!\[' -or $line -match '^\s*\[[^]]+\]\(README' -or $line -match '^\s*\[[^]]+\]\(https?://';$out.Add($line);if(-not $skip){$batch.Add($line);$idx.Add($out.Count-1);if($batch.Count -ge 20){FlushBatch}}};FlushBatch
  Set-Content (Join-Path $Root "README.$locale.md") $out -Encoding utf8;Write-Host "translated $locale"
}
