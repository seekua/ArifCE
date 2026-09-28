# ArifCE
<p align="center"><img src="../../assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

**Ler em outro idioma.**

[English](../../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

**Agentes mudam. Seu projeto não deve esquecer.**


[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](../../LICENSE)

ArifCE é uma camada local de inteligência e continuidade do projeto para desenvolvimento de software assistido por IA. Mantém contexto, decisões, tentativas malsucedidas, evidências, estado de refatoração e informações de transição no repositório para que Codex, Claude Code, OpenCode e futuros agentes continuem a mesma história de engenharia.

> O repositório é dono do contexto. O agente apenas o toma emprestado.

**Seu limite acabou? Continue em dois comandos.**

Após concluir o trabalho real em uma tarefa existente, registre a transferência (*handoff*) antes de parar.

```bash
arifce handoff --task TASK-0031

# When you return with Codex, Claude Code, OpenCode, or a local model:
arifce context --task TASK-0031 --budget 2000
```

A transferência contém o objetivo, o trabalho concluído, as evidências verificadas, os itens pendentes, as falhas e a próxima ação. Copie a saída de contexto impressa para o prompt de inicialização do novo agente; a CLI não injeta contexto automaticamente na sessão do modelo.

## Instalação e início rápido

Baixe o pacote autônomo para sua plataforma em [GitHub Releases](https://github.com/seekua/ArifCE/releases/tag/v0.8.1), extraia-o e adicione o `arifce` ao seu PATH. No Linux, preserve a permissão de execução ao extrair ou execute `chmod +x arifce`. Não é necessária nenhuma instalação separada de .NET, Node, Python, Docker ou banco de dados.

Para um novo projeto:

```bash
mkdir my-project && cd my-project
git init
arifce init
arifce task create "Ship the first change"
arifce handoff
```

Para um repositório Git existente:

```bash
cd path/to/existing-repo
arifce adopt
arifce task create "Ship the first change"
arifce handoff
```

Use `adopt` quando o repositório já contiver código: ele registra a estrutura observada sem sobrescrevê-la e fornece ao próximo agente um ponto de partida local do projeto. [Instalação e início rápido](../getting-started/installation.md).

## Por que o ArifCE existe

Equipes de software perdem tempo e confiança quando o contexto importante vive apenas no histórico de chat, na memória individual ou em uma ferramenta que o próximo colaborador não pode inspecionar. O ArifCE torna a continuidade da engenharia parte do próprio projeto.

O objetivo não é fazer os agentes parecerem mais certos. É ajudar cada colaborador a entender o que a equipe tenta realizar, por que uma decisão foi tomada, o que foi realmente verificado e onde permanece a incerteza. Quando essa história fica no repositório, as equipes avançam mais rápido sem abrir mão de rastreabilidade, responsabilidade ou confiança.

O ArifCE transforma a continuidade em uma prática de engenharia compartilhada: contexto focado para a próxima tarefa, evidências explícitas para afirmações importantes e transições honestas quando o trabalho está incompleto.

**Para quem é.**

O ArifCE é para equipes de engenharia assistidas por IA, desenvolvedores que trabalham com agentes de código e mantenedores que precisam que o contexto do projeto sobreviva a uma pessoa, conversa ou sessão. É especialmente útil quando vários colaboradores compartilham um repositório e precisam de um registro claro de decisões, verificações e trabalho inacabado.

## Como o ArifCE funciona

```mermaid
flowchart LR
    A[Agente inicia] --> B[Ler protocolo e estado atual]
    B --> C[Recuperar contexto da tarefa]
    C --> D[Alterar o código]
    D --> E[Registrar afirmação e evidência]
    E --> F{Verificação aprovada?}
    F -- Sim --> G[Ponto de controle e transição]
    F -- Não --> H[Registrar descoberta ou tentativa malsucedida]
    H --> C
    G --> I[Próximo agente continua]
```

## Explore o projeto

Execute o painel local para obter uma visão visual da saúde do projeto, dos registros recentes e do contexto pesquisável: Este comando para desenvolvedores utiliza o SDK do .NET; a instalação da versão autônoma descrita acima não o exige.

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Depois abra <http://127.0.0.1:5180/>. Para o manual completo do produto, consulte o [hub de documentação do ArifCE](../README.md).

Esse fluxo mantém o conhecimento do projeto no repositório e torna o progresso inspecionável. As vantagens práticas são:

- Onboarding mais rápido: o próximo agente lê um estado atual focado em vez de reconstruir uma longa transcrição.
- Mudanças mais seguras: afirmações são vinculadas a evidências determinísticas e ficam obsoletas quando o estado do Git muda.
- Melhor continuidade: decisões, tentativas malsucedidas, pontos de controle e transições sobrevivem a mudanças de agente ou sessão.
- Refatorações controladas: invariantes, inventário, proteções e pontos seguros tornam o trabalho incompleto visível.
- Operação local: arquivos canônicos continuam utilizáveis sem serviço em nuvem ou runtime específico do fornecedor.

## Não é apenas memória

O ArifCE acompanha qual era a tarefa, o que mudou e por quê, o que um agente afirma ter concluído, quais evidências sustentam a afirmação, o que um revisor encontrou, o que permanece inacabado e o que o próximo agente precisa saber. Declarações de agentes são afirmações, não fatos; evidências determinísticas de build, teste, Git e busca são preferidas.

A verificação técnica e a aceitação do produto são separadas: registros de aceitação identificam quem aprovou uma afirmação e quais evidências atuais sustentaram a decisão.

## Fluxo de trabalho principal

```text
arifce init
arifce task create "Fix permission cache race"
arifce checkpoint --summary "Reproduction added"
arifce context "finish the permission cache fix" --budget 16000
arifce claim create "Permission cache race is fixed"
arifce verify CLAIM-0001
arifce handoff
```

Markdown, YAML, JSON e JSONL canônicos ficam em `.arifce/`. SQLite é um índice derivado descartável: excluir `.arifce/index/` e executar `arifce rebuild` deve preservar a inteligência do projeto.

## Arquitetura

O núcleo separa as regras de domínio, o armazenamento e a indexação canônicos, a observação do Git, a recuperação, a verificação, a refatoração, a segurança e a CLI. Os arquivos de instruções dos fornecedores são pequenos adaptadores; nunca se tornam o armazenamento de memória canônico. Consulte a [visão geral da arquitetura](../architecture/overview.md), o [modelo de domínio](../architecture/domain-model.md) e a [especificação V0.1](../SPECIFICATION-v0.1.md).

**Desenvolvimento a partir do código-fonte. A versão atual é a V0.8.1. Para desenvolvimento a partir do código-fonte, consulte as instruções de instalação e o guia de início rápido.** [Instalação e início rápido](../getting-started/installation.md) · [Início rápido](../getting-started/quick-start.md).

```bash
git clone https://github.com/seekua/ArifCE.git
cd ArifCE
dotnet restore ArifCE.slnx
dotnet build ArifCE.slnx --configuration Release --no-restore
dotnet test ArifCE.slnx --configuration Release --no-build --no-restore
```

O adaptador MCP local opcional está documentado em [configuração do MCP](../getting-started/mcp.md).

Para um guia completo de instalação e recursos, consulte o [Guia do usuário](../USER-GUIDE.md) e a [Política de documentação](../DOCUMENTATION-POLICY.md).

Os comandos de instalação e inicialização acima criam um estado de projeto local ao repositório, uma tarefa e uma transferência pronta para o próximo colaborador.

### Continue uma tarefa com Ollama ou LM Studio

O ArifCE mantém os registros canônicos do projeto no repositório. O provedor recebe o prompt e o contexto selecionado; provedores na nuvem recebem esse conteúdo selecionado remotamente. `--with-context` adiciona os registros do projeto selecionados pelo ArifCE, mas não lê arquivos-fonte. Os exemplos abaixo incluem explicitamente o conteúdo do arquivo de migração no prompt, para que o modelo receba o código que deve analisar. Use o exemplo correspondente ao seu shell.

Antes de usar qualquer exemplo, instale e inicie o Ollama. O primeiro comando baixa o modelo `llama3`; mantenha o Ollama em execução no endpoint local indicado abaixo.

```bash
ollama pull llama3
arifce llm provider add ollama Ollama llama3 --endpoint http://127.0.0.1:11434
arifce llm provider test ollama
task_id="$(arifce task create "Review and safely update the migration")"
migration_source="$(cat path/to/migration.sql)"
prompt="Review only this SQL migration for data-loss risks. Do not infer unseen source. Suggest the smallest safe change if needed.
Migration source:
$migration_source"
arifce llm run "Review and safely update the migration" "$prompt" --with-context --budget 2000
```

```powershell
ollama pull llama3
arifce llm provider add ollama Ollama llama3 --endpoint http://127.0.0.1:11434
arifce llm provider test ollama
$taskId = arifce task create "Review and safely update the migration"
$migrationSource = Get-Content -Raw -LiteralPath "path/to/migration.sql"
$prompt = @"
Review only this SQL migration for data-loss risks. Do not infer unseen source. Suggest the smallest safe change if needed.
Migration source:
$migrationSource
"@
arifce llm run "Review and safely update the migration" $prompt --with-context --budget 2000
```

Revise a resposta do modelo, aplique as alterações sugeridas no seu ambiente de desenvolvimento e execute os testes de regressão da migração. Depois que passarem, registre como claim somente o que o comando de teste comprova:

```bash
claim_id="$(arifce claim create "Migration regression tests pass" --task "$task_id")"
arifce verify "$claim_id" --command "dotnet test path/to/migration-tests.csproj"
arifce handoff
```

No PowerShell:

```powershell
$claimId = arifce claim create "Migration regression tests pass" --task $taskId
arifce verify $claimId --command "dotnet test path/to/migration-tests.csproj"
arifce handoff
```

O resultado do teste dá suporte ao claim de que os testes de regressão passaram; sozinho, ele não prova que a análise do modelo foi completa ou correta. Substitua os caminhos e o comando de teste de exemplo pelos do seu repositório. Para usar o LM Studio, informe o nome do modelo carregado e o endpoint compatível com OpenAI, geralmente `http://127.0.0.1:1234/v1`, em `arifce llm provider add lmstudio LmStudio your-loaded-model --endpoint http://127.0.0.1:1234/v1`.

Depois, siga o mesmo fluxo de tarefa, entrada do código-fonte, evidência dos testes e handoff.

Executar um reviewer exige aprovação explícita. A referência de [provedores LLM](../reference/LLM-PROVIDERS.md) documenta o fallback de provedores, o registro de tokens e custos, as evidências canônicas, os embeddings, as métricas de benchmark, as ferramentas MCP e o dashboard local.

Execute `init` em um repositório Git novo ou `adopt` em um existente. Ambos são não destrutivos e idempotentes; `adopt` registra a estrutura observada e marca como desconhecidas as justificativas históricas que não são conhecidas.

## Continuidade, verificação e refatorações

- Um agente novo lê `AGENTS.md`, `.arifce/PROTOCOL.md` e `.arifce/CURRENT.md`, depois solicita contexto específico da tarefa em vez de carregar todo o histórico.
- Afirmações apontam para evidências do repositório. As evidências ficam obsoletas quando o estado relevante muda.
- Campanhas de refatoração acompanham invariantes, inventário, proteções, progresso e pontos de controle. Proteções bloqueadoras impedem a conclusão.
- Transições resumem o estado atual da engenharia em vez de despejar transcrições.

## Segurança e limitações

Transcrições brutas não são confiáveis ​​e nunca são carregadas em massa ou executadas. Os caminhos de importação ocultam segredos comuns; credenciais e dados de autenticação da máquina não devem constar no arquivo `.arifce`. O ArifCE não garante correção, economia de tokens ou melhor qualidade de revisão. Ele não possui serviço em nuvem, interface hospedada, banco de dados vetorial, enxame autônomo ou invocação entre agentes em produção. Um painel local está incluído; não se trata de uma aplicação web hospedada.

Consulte [ROADMAP.md](../../ROADMAP.md), [SECURITY.md](../../SECURITY.md) e [CONTRIBUTING.md](../../CONTRIBUTING.md). A sintaxe exata dos comandos implementados está documentada na [referência da CLI](../reference/cli.md).

## Licença

O ArifCE é distribuído sob a [licença Apache 2.0](../../LICENSE).
