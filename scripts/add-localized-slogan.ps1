$map = @{
  'ar'=@('الوكلاء يتغيرون. يجب ألا ينسى مشروعك.','المستودع يملك السياق. الوكيل يستعيره فقط.')
  'bn'=@('এজেন্ট বদলায়। আপনার প্রকল্প যেন না ভোলে।','রিপোজিটরিই প্রেক্ষাপটের মালিক। এজেন্ট কেবল তা ধার নেয়।')
  'bs'=@('Agenti se mijenjaju. Vaš projekat ne smije zaboraviti.','Repozitorij posjeduje kontekst. Agent ga samo posuđuje.')
  'da'=@('Agenter ændrer sig. Dit projekt bør ikke glemme.','Repositoriet ejer konteksten. Agenten låner den kun.')
  'de'=@('Agenten wechseln. Ihr Projekt sollte nicht vergessen.','Das Repository besitzt den Kontext. Der Agent leiht ihn nur.')
  'el'=@('Οι πράκτορες αλλάζουν. Το έργο σας δεν πρέπει να ξεχνά.','Το αποθετήριο κατέχει το πλαίσιο. Ο πράκτορας απλώς το δανείζεται.')
  'es'=@('Los agentes cambian. Tu proyecto no debería olvidar.','El repositorio es dueño del contexto. El agente solo lo toma prestado.')
  'fr'=@('Les agents changent. Votre projet ne doit pas oublier.','Le dépôt possède le contexte. L’agent ne fait que l’emprunter.')
  'it'=@('Gli agenti cambiano. Il tuo progetto non deve dimenticare.','Il repository possiede il contesto. L’agente lo prende solo in prestito.')
  'ja'=@('エージェントは変わる。プロジェクトは忘れてはいけない。','リポジトリがコンテキストを所有し、エージェントはそれを借りるだけです。')
  'ko'=@('에이전트는 바뀝니다. 프로젝트는 잊지 않아야 합니다.','저장소가 컨텍스트를 소유하고 에이전트는 그것을 빌릴 뿐입니다.')
  'no'=@('Agenter endrer seg. Prosjektet ditt bør ikke glemme.','Repositoriet eier konteksten. Agenten låner den bare.')
  'pl'=@('Agenci się zmieniają. Twój projekt nie powinien zapominać.','Repozytorium posiada kontekst. Agent tylko go wypożycza.')
  'pt-BR'=@('Agentes mudam. Seu projeto não deve esquecer.','O repositório é dono do contexto. O agente apenas o toma emprestado.')
  'ru'=@('Агенты меняются. Проект не должен забывать.','Репозиторий владеет контекстом. Агент лишь берёт его взаймы.')
  'th'=@('เอเจนต์เปลี่ยนแปลง โครงการของคุณไม่ควรลืม','รีโพซิทอรีเป็นเจ้าของบริบท เอเจนต์เพียงยืมไปใช้')
  'tr'=@('Ajanlar değişir. Projen unutmamalı.','Bağlamın sahibi repodur. Ajan yalnızca onu ödünç alır.')
  'uk'=@('Агенти змінюються. Проєкт не повинен забувати.','Репозиторій володіє контекстом. Агент лише позичає його.')
  'vi'=@('Agent thay đổi. Dự án của bạn không nên quên.','Repository sở hữu ngữ cảnh. Agent chỉ mượn nó.')
  'zh-CN'=@('代理会更替，项目不应遗忘。','仓库拥有上下文，代理只是借用它。')
  'zh-TW'=@('代理會更替，專案不應遺忘。','儲存庫擁有上下文，代理只是借用它。')
}
foreach($f in Get-ChildItem README.*.md | Where-Object Name -ne 'README.md') {
  $key=$f.BaseName.Substring(7); if(!$map.ContainsKey($key)){continue}
  $t=Get-Content $f.FullName -Raw; if($t -match '\*\*Agents change|\*\*Ajanlar değişir'){continue}
  $lines=$t -split "`r?`n"; $idx=($lines | Select-String '^\[English\]\(README\.md\)' | Select-Object -First 1).LineNumber-1
  if($idx -ge 0){$ins=@('','**'+$map[$key][0]+'**','', '> '+$map[$key][1]); $lines=@($lines[0..$idx]+$ins+$lines[($idx+1)..($lines.Length-1)]); [IO.File]::WriteAllText($f.FullName,($lines -join "`r`n"))}
}
