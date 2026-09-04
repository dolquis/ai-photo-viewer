> [!IMPORTANT]
> **このリポジトリは開発を中止しました（2026-09-04）。アーカイブののち削除します。**
>
> - Linear Project: [AI Photo Viewer Local AI Photo MVP](https://linear.app/dolquis/project/ai-photo-viewer-local-ai-photo-mvp-575ebeafac1e)（Canceled）
> - 設計文書・Linear の記録の退避先: `dolquis/agent-ops` の [`docs/archive/ai-photo-viewer/`](https://github.com/dolquis/agent-ops/tree/main/docs/archive/ai-photo-viewer)
> - 削除予定: アーカイブから 30 日後
>
> 以下の記述は中止時点のもので、更新しません。

# ai-photo-viewer

オンデバイス AI 写真ビューワー／整理アプリ（Windows ファースト、クロスプラットフォーム核）。

単なる画像ビューワーではなく、ローカル AI を用いて画像検索・重複検知・類似画像検索・
自動タグ付け・顔検出/人物グループ化・OCR・画質診断・軽度な AI 補正を行う写真整理アプリ。
クラウド API に依存せず、ネットワーク非接続で主要機能が動作する。

## ステータス

開発中止。中止時点では設計ドキュメントと .NET ソリューション雛形までで、
`docs/roadmap.md` の Phase 0（技術検証）には着手していない。

## 開発フロー（Linear 連携）

本リポジトリは **Linear を管制塔（control tower）**、**GitHub の `docs/` を真実の源** として開発する。
作業の規約（ビルド/テスト・レイヤ依存・ブランチ/PR・PoC の扱い）は **[AGENTS.md](AGENTS.md)** に集約しているので、着手前に必ず読むこと。

- Issue 単位で作業し、各 Issue の Linear ブランチ（`dolquis/dev-NN-...`）で開発する。
- PR は本文に対象 Issue（`DEV-NN`）を必ずリンクし、最初はドラフトで作成する。
- CI（`.github/workflows/ci.yml`）が `dotnet build` / `dotnet test` を検査する。

### Phase（roadmap）↔ Stage（Linear）対応

| roadmap.md | Linear | 内容 |
|---|---|---|
| Phase 0 | P0 Technical validation | 技術検証 PoC（`poc/`） |
| Phase 1 | Stage 1 core viewer | 高速ビューワー + DB + サムネイル |
| Phase 2 | Stage 2 search | 重複検知 + 類似画像検索 |
| Phase 3 | Stage 3 language and tags | 自然言語検索 + 自動タグ |
| Phase 4 | Stage 4 people grouping | 顔検出 + 人物グループ化（MVP 完成） |
| Phase 5〜7 | （MVP 後） | OCR / 画質診断 / 補正・共有前チェック |

## ドキュメント

| ファイル | 内容 |
|---|---|
| [AGENTS.md](AGENTS.md) | 開発・エージェント運用ガイド（Linear 連携 / ビルド / 規約） |
| [docs/tech-selection.md](docs/tech-selection.md) | 技術選定レポート（UI / 言語 / 推論ランタイム / DB・ベクトル検索） |
| [docs/mvp-spec.md](docs/mvp-spec.md) | MVP 仕様書（機能・画面・ユースケース・受け入れ条件） |
| [docs/architecture.md](docs/architecture.md) | アプリケーション設計書（アーキテクチャ・DB・ジョブ・ログ） |
| [docs/model-management.md](docs/model-management.md) | AI モデル管理設計 |
| [docs/privacy.md](docs/privacy.md) | プライバシー・安全設計 |
| [docs/roadmap.md](docs/roadmap.md) | 実装ロードマップ（Phase 0〜7） |
| [docs/poc-tasks.md](docs/poc-tasks.md) | 技術検証タスク一覧 |

## 推奨技術スタック

Avalonia UI 11.x / C#/.NET 8 / ONNX Runtime / SQLite + HNSW ベクトル索引。
選定根拠は `docs/tech-selection.md` を参照。

## リポジトリ構成

```text
src/
  App/            Avalonia アプリ起動・View・DI 構成
  UI/             ViewModel（推論実装に非依存）
  Core/           ドメインモデル
  Infrastructure/ ファイル監視・設定・ロギング
  Database/       SQLite リポジトリ
  Imaging/        サムネイル生成・知覚ハッシュ
  AI/             推論サービス抽象（ONNX Runtime）
  Jobs/           バックグラウンドジョブキュー
  Search/         ベクトル索引・検索ファサード
tests/
  AiPhotoViewer.Tests   ユニット/結合テスト
poc/              Phase 0 技術検証コード（使い捨て・sln 非同梱。poc/README.md 参照）
docs/             設計ドキュメント
models/           AI モデル配置先（モデル本体は管理外。docs/model-management.md 参照）
```

各プロジェクトの責務は `docs/architecture.md` 2章を参照。

## ビルド

.NET 8 SDK が必要。

```sh
dotnet build AiPhotoViewer.sln
dotnet test
```

現状の各プロジェクトはドメインモデルと層間インターフェースの雛形のみで、
業務ロジックは未実装。実装は Phase 0 以降で順次追加する。
