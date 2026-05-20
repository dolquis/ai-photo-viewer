# ai-photo-viewer

オンデバイス AI 写真ビューワー／整理アプリ（Windows ファースト、クロスプラットフォーム核）。

単なる画像ビューワーではなく、ローカル AI を用いて画像検索・重複検知・類似画像検索・
自動タグ付け・顔検出/人物グループ化・OCR・画質診断・軽度な AI 補正を行う写真整理アプリ。
クラウド API に依存せず、ネットワーク非接続で主要機能が動作する。

## ステータス

設計フェーズ。設計ドキュメントと .NET ソリューション雛形を整備済み。
実装は `docs/roadmap.md` の Phase 0（技術検証）から着手する。

## ドキュメント

| ファイル | 内容 |
|---|---|
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
