# ArifCE
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](LICENSE)

ArifCE হলো AI-সহায়িত সফটওয়্যার উন্নয়নের জন্য স্থানীয়-প্রথম প্রকল্প বুদ্ধিমত্তা ও ধারাবাহিকতার স্তর। এটি রিপোজিটরিতে প্রেক্ষাপট, সিদ্ধান্ত, ব্যর্থ প্রচেষ্টা, প্রমাণ, রিফ্যাক্টরিং অবস্থা ও হস্তান্তর তথ্য রাখে, যাতে Codex, Claude Code, OpenCode এবং ভবিষ্যৎ এজেন্ট একই প্রকৌশল কাহিনি চালিয়ে যেতে পারে।

> রিপোজিটরিই প্রেক্ষাপটের মালিক। এজেন্ট কেবল তা ধার নেয়।

## ArifCE কেন বিদ্যমান

গুরুত্বপূর্ণ প্রেক্ষাপট যখন শুধু চ্যাট ইতিহাস, ব্যক্তিগত স্মৃতি বা পরবর্তী অবদানকারী যে সরঞ্জামটি পরীক্ষা করতে পারে না তাতে থাকে, তখন সফটওয়্যার দল সময় ও আস্থা হারায়। ArifCE প্রকল্পের নিজস্ব অংশ হিসেবে প্রকৌশল ধারাবাহিকতা তৈরি করে।

লক্ষ্য এজেন্টদের আরও নিশ্চিত শোনানো নয়; প্রত্যেক অবদানকারী যেন দলের উদ্দেশ্য, সিদ্ধান্তের কারণ, যাচাইকৃত বিষয় এবং অবশিষ্ট অনিশ্চয়তা বোঝে সেটিই লক্ষ্য। এই ইতিহাস রিপোজিটরিতে থাকলে দল স্বচ্ছতা, দায়িত্ব ও আস্থা বজায় রেখে দ্রুত এগোতে পারে।

ArifCE ধারাবাহিকতাকে যৌথ প্রকৌশল অনুশীলনে রূপ দেয়: পরবর্তী কাজের জন্য কেন্দ্রীভূত প্রেক্ষাপট, গুরুত্বপূর্ণ দাবির স্পষ্ট প্রমাণ এবং কাজ অসম্পূর্ণ হলে সৎ হস্তান্তর।

## কার জন্য

ArifCE AI-সহায়িত প্রকৌশল দল, কোডিং এজেন্ট ব্যবহারকারী ডেভেলপার এবং এমন রক্ষণাবেক্ষণকারীদের জন্য, যাদের প্রকল্পের প্রেক্ষাপট একজন ব্যক্তি, চ্যাট বা সেশনের পরেও টিকে থাকা দরকার। একাধিক অবদানকারী একই রিপোজিটরি ভাগ করলে এটি বিশেষভাবে উপযোগী।

## ArifCE কীভাবে কাজ করে

```mermaid
flowchart LR
    A[এজেন্ট শুরু] --> B[প্রোটোকল ও বর্তমান অবস্থা পড়ুন]
    B --> C[কাজ-নির্দিষ্ট প্রসঙ্গ আনুন]
    C --> D[কোড পরিবর্তন করুন]
    D --> E[দাবি ও প্রমাণ নথিভুক্ত করুন]
    E --> F{যাচাই সফল?}
    F -- Yes --> G[চেকপয়েন্ট ও হস্তান্তর]
    F -- No --> H[ফলাফল বা ব্যর্থ প্রচেষ্টা নথিভুক্ত করুন]
    H --> C
    G --> I[পরবর্তী এজেন্ট চালিয়ে যায়]
```

## প্রকল্প অন্বেষণ

Run the local dashboard to get a visual overview of project health, recent records, and searchable context:

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Then open <http://127.0.0.1:5180/>. For the complete product handbook, see the [ArifCE documentation hub](docs/README.md).

This workflow keeps project knowledge in the repository and makes progress inspectable. The practical advantages are:

- দ্রুত শুরু: পরবর্তী এজেন্ট দীর্ঘ ট্রান্সক্রিপ্ট পুনর্গঠন না করে কেন্দ্রীভূত বর্তমান অবস্থা পড়ে।
- নিরাপদ পরিবর্তন: দাবিগুলো নির্ধারিত প্রমাণের সঙ্গে যুক্ত এবং Git অবস্থা বদলালে পুরোনো হয়ে যায়।
- ভালো ধারাবাহিকতা: সিদ্ধান্ত, ব্যর্থ প্রচেষ্টা, চেকপয়েন্ট ও হস্তান্তর এজেন্ট বা সেশন পরিবর্তনের পরেও থাকে।
- নিয়ন্ত্রিত রিফ্যাক্টরিং: ইনভেরিয়েন্ট, তালিকা, গার্ড ও নিরাপদ পয়েন্ট অসম্পূর্ণ কাজ দৃশ্যমান করে।
- স্থানীয়-প্রথম পরিচালনা: ক্লাউড পরিষেবা বা নির্দিষ্ট রানটাইম ছাড়াই মূল ফাইল ব্যবহারযোগ্য থাকে।

## শুধু স্মৃতি নয়

ArifCE কাজের বিষয়, কী বদলেছে ও কেন, এজেন্ট কী সম্পন্ন করার দাবি করছে, সেই দাবির প্রমাণ, পর্যালোচকের ফলাফল, অসম্পূর্ণ অংশ এবং পরবর্তী এজেন্টের প্রয়োজনীয় তথ্য অনুসরণ করে। এজেন্টের বক্তব্য দাবি, সত্য নয়; নির্ধারিত build, test, Git ও search প্রমাণ অগ্রাধিকার পায়।

প্রযুক্তিগত যাচাই ও পণ্য গ্রহণ আলাদা: গ্রহণ রেকর্ডে কে দাবিটি অনুমোদন করেছে এবং কোন বর্তমান প্রমাণ সিদ্ধান্তটিকে সমর্থন করেছে তা থাকে।

## V0.1 workflow

```text
arifce init
arifce task create "Fix permission cache race"
arifce checkpoint --summary "Reproduction added"
arifce context "finish the permission cache fix" --budget 16000
arifce claim create "Permission cache race is fixed"
arifce verify CLAIM-0001
arifce handoff
```

Canonical Markdown, YAML, JSON, and JSONL live under `.arifce/`. SQLite is a disposable derived index: deleting `.arifce/index/` and running `arifce rebuild` must preserve project intelligence.

## আর্কিটেকচার

মূল অংশটি ডোমেইন নিয়ম, ক্যানোনিকাল স্টোরেজ ও ইনডেক্স, Git পর্যবেক্ষণ, পুনরুদ্ধার, যাচাই, রিফ্যাক্টরিং, নিরাপত্তা ও CLI আলাদা রাখে। সরবরাহকারীর নির্দেশনা ফাইল ছোট অ্যাডাপ্টার; এগুলো কখনও ক্যানোনিকাল মেমরি স্টোর হয় না।

## Installation and quick start

V0.2.0 is published as a cross-platform .NET global tool. See [installation](docs/getting-started/installation.md) and the [quick start](docs/getting-started/quick-start.md). From source:

The optional local MCP adapter is documented in [MCP setup](docs/getting-started/mcp.md).

For a complete installation and feature walkthrough, see the [User Guide](docs/USER-GUIDE.md) and [Documentation Policy](docs/DOCUMENTATION-POLICY.md).

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

You now have a repository-local project state, a task, a checkpoint, and a semantic handoff ready for the next contributor.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ArifCE.Cli -- init
```

Run `init` in a new Git repository or `adopt` in an existing one. Both are non-destructive and idempotent. `adopt` records observed structure and labels unknown historical rationale as unknown.

## Continuity, verification, and refactors

- A fresh agent reads `AGENTS.md`, `.arifce/PROTOCOL.md`, and `.arifce/CURRENT.md`, then requests task-specific context instead of bulk-loading history.
- Claims link to repository-scoped evidence. Evidence becomes stale when the relevant repository state changes.
- Refactor campaigns track invariants, inventory, guards, progress, and checkpoints. Blocking guards prevent completion.
- Handoffs summarize current engineering state rather than dumping transcripts.

## নিরাপত্তা ও সীমাবদ্ধতা

কাঁচা ট্রান্সক্রিপ্ট অবিশ্বস্ত; এগুলো কখনও একসঙ্গে লোড বা চালানো হয় না। ইমপোর্ট পাথ সাধারণ গোপনীয়তা গোপন করে; শংসাপত্র ও মেশিন প্রমাণীকরণ তথ্য `.arifce/`-এ রাখা যাবে না। V0.1 সঠিকতা, টোকেন সাশ্রয় বা উন্নত রিভিউ মানের নিশ্চয়তা দেয় না এবং এতে ক্লাউড, UI, ভেক্টর ডেটাবেস, স্বয়ংক্রিয় swarm বা উৎপাদন এজেন্ট-কল নেই।

See [ROADMAP.md](ROADMAP.md), [SECURITY.md](SECURITY.md), and [CONTRIBUTING.md](CONTRIBUTING.md). The exact implemented command syntax is documented in the [CLI reference](docs/reference/cli.md).

## লাইসেন্স

ArifCE is licensed under the [Apache License 2.0](LICENSE).
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>




