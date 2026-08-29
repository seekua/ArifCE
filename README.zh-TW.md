# ArifCE
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

**代理會更替，專案不應遺忘。**


[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](LICENSE)

ArifCE 是面向 AI 輔助軟體開發的本地優先專案智慧與連續性層。它將脈絡、決策、失敗嘗試、證據、重構狀態與交接資訊保存在儲存庫中，讓 Codex、Claude Code、OpenCode 及未來的代理延續同一段工程歷程。
> 儲存庫擁有上下文，代理只是借用它。


## ArifCE 為何存在

當重要脈絡只存在於聊天記錄、個人記憶或下一位貢獻者無法檢查的工具中，軟體團隊會失去時間與信心。ArifCE 讓工程連續性成為專案本身的一部分。

目標不是讓代理聽起來更確定，而是幫助每位貢獻者了解團隊要完成什麼、為何做出決策、哪些內容確實驗證過，以及哪裡仍存在不確定性。當這段歷程留在儲存庫中，團隊不必放棄可追溯性、責任或信任即可更快前進。

ArifCE 將連續性轉化為共同的工程實務：為下一項任務提供聚焦脈絡，為重要聲明提供明確證據，並在工作未完成時進行誠實交接。

## 適用對象

ArifCE 適用於 AI 輔助工程團隊、使用編碼代理的開發者，以及希望專案脈絡超越個人、聊天或工作階段持續存在的維護者。當多人共用儲存庫並需要清楚記錄決策、驗證與未完成工作時尤其有用。

## ArifCE 如何運作

```mermaid
flowchart LR
    A[代理開始] --> B[讀取協定與目前狀態]
    B --> C[取得任務脈絡]
    C --> D[修改程式碼]
    D --> E[記錄聲明與證據]
    E --> F{驗證通過？}
    F -- 是 --> G[檢查點與交接]
    F -- 否 --> H[記錄發現或失敗嘗試]
    H --> C
    G --> I[下一位代理繼續]
```

## 探索專案

執行本機儀表板，以視覺化檢視專案健康狀況、近期記錄與可搜尋脈絡：

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

接著開啟 <http://127.0.0.1:5180/>。完整產品手冊請參閱 [ArifCE 文件中心](docs/README.md)。

此流程將專案知識保留在儲存庫中，讓進度可供檢查。實際優點包括：

- 更快上手：下一位代理讀取聚焦的目前狀態，不必重建冗長記錄。
- 更安全的變更：聲明連結至確定性證據，Git 狀態改變後會失效。
- 更佳連續性：決策、失敗嘗試、檢查點與交接可跨代理或工作階段保留。
- 受控重構：不變量、清單、防護與安全點讓未完成工作清晰可見。
- 本機優先運作：規範檔案不需雲端服務或供應商專用執行環境即可使用。

## 不只是記憶

ArifCE 追蹤任務內容、變更及原因、代理聲稱完成的事項、支持該聲明的證據、審查者的發現、未完成事項及下一位代理需要知道的資訊。代理陳述是聲明而非事實；優先採用確定性的建置、測試、Git 與搜尋證據。

技術驗證與產品驗收彼此獨立：驗收記錄會標示誰核准了聲明，以及哪些目前證據支持該決定。

## V0.1 工作流程

```text
arifce init
arifce task create "Fix permission cache race"
arifce checkpoint --summary "Reproduction added"
arifce context "finish the permission cache fix" --budget 16000
arifce claim create "Permission cache race is fixed"
arifce verify CLAIM-0001
arifce handoff
```

規範的 Markdown、YAML、JSON 與 JSONL 位於 `.arifce/`。SQLite 是可丟棄的衍生索引：刪除 `.arifce/index/` 並執行 `arifce rebuild` 必須保留專案智慧。

## 架構

核心將領域規則、規範儲存與索引、Git 觀察、擷取、驗證、重構、安全性與 CLI 分離。供應商指示檔只是小型介面卡，絕不會成為規範記憶儲存庫。請參閱[架構概覽](docs/architecture/overview.md)、[領域模型](docs/architecture/domain-model.md)及 [V0.1 規格](docs/SPECIFICATION-v0.1.md)。

## 安裝與快速開始

V0.2.0 已作為跨平台 .NET 全域工具發布。請參閱[安裝](docs/getting-started/installation.md)與[快速開始](docs/getting-started/quick-start.md)。從原始碼執行：

選用的本機 MCP 配接器記載於 [MCP 設定](docs/getting-started/mcp.md)。

完整的安裝與功能說明請參閱[使用者指南](docs/USER-GUIDE.md)和[文件政策](docs/DOCUMENTATION-POLICY.md)。

### 60-second quick start

```bash
dotnet tool install --global ArifCE.Cli --version 0.2.0
mkdir my-project && cd my-project
git init
arifce init
arifce task create "Ship the first change"
arifce checkpoint --summary "Project context initialized"
arifce handoff
```

現在你已有儲存庫本機專案狀態、任務、檢查點，以及可交給下一位貢獻者的語義交接。

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ArifCE.Cli -- init
```

在新的 Git 儲存庫中執行 `init`，或在現有儲存庫中執行 `adopt`。兩者都是非破壞性且具冪等性。`adopt` 會記錄觀察到的結構，並將未知的歷史原因標記為未知。

## 連續性、驗證與重構

- 新代理讀取 `AGENTS.md`、`.arifce/PROTOCOL.md` 與 `.arifce/CURRENT.md`，再要求任務脈絡而非批次載入歷史。
- 聲明連結至儲存庫範圍的證據；相關狀態變更後證據會失效。
- 重構活動追蹤不變量、清單、防護、進度與檢查點；阻擋性防護會阻止完成。
- 交接會摘要目前工程狀態，而不是傾倒逐字記錄。

## 安全性與限制

原始記錄不受信任，絕不會批次載入或執行。匯入路徑會遮蔽常見祕密；憑證和機器驗證資料不得放入 `.arifce/`。V0.1 不保證正確性、節省 token 或提升審查品質，也不提供雲端服務、UI、向量資料庫、自主群體或正式環境代理間呼叫。

請參閱 [ROADMAP.md](ROADMAP.md)、[SECURITY.md](SECURITY.md) 和 [CONTRIBUTING.md](CONTRIBUTING.md)。已實作命令的確切語法記載於 [CLI 參考](docs/reference/cli.md)。

## 授權條款

ArifCE 依據 [Apache License 2.0](LICENSE) 授權。