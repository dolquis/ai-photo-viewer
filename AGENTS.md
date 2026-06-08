# エージェント運用ガイド（AGENTS.md）

本リポジトリは **Linear を管制塔（control tower）** とし、**GitHub の `docs/` を真実の源（source of truth）** として開発する。
人間・Claude・Codex のいずれが作業する場合も、本書の規約に従うこと。

> このファイルは Codex / Claude Code 双方が参照する正本。`CLAUDE.md` は本書への入口。

---

## 1. プロジェクト概要

オンデバイス AI 写真ビューワー（Windows ファースト、クロスプラットフォーム核）。
設計の詳細は以下を読むこと（着手前に必読）。

| ドキュメント | 内容 |
|---|---|
| `docs/poc-tasks.md` | Phase 0 技術検証 PoC 一覧（PoC-1〜8） |
| `docs/tech-selection.md` | 技術選定（UI / 言語 / 推論ランタイム / DB・ベクトル検索） |
| `docs/architecture.md` | レイヤード構成・AI パイプライン・DB・ジョブ・ログ |
| `docs/mvp-spec.md` | MVP 仕様・受け入れ条件（AC-x） |
| `docs/roadmap.md` | Phase 0〜7 |
| `docs/model-management.md` / `docs/privacy.md` | モデル管理 / プライバシー |

---

## 2. Linear 連携（必須）

- **Issue が作業単位**。実装は `agent:codex-impl`、設計・整合確認は `agent:claude-*` の Issue で受ける。
- **ブランチは Linear のネイティブ連携に寄せる**。各 Issue の `gitBranchName`（例: `dolquis/dev-42-photo-image-view-poc`）でブランチを切る。
  - Claude Code web 由来の `claude/...` ブランチで作業した場合も、**PR 本文に対象 Issue を必ずリンク**して連携を確保する。
- **PR は Issue にひも付ける**。本文に Linear のマジックワードを入れる:
  - 完了で Issue を閉じる場合: `Fixes DEV-42`
  - 関連のみ: `Part of DEV-40` / `Refs DEV-41`
- **状態運用**: 着手で `In Progress`、PR 作成で `In Review`、マージで `Done`。`type:tracking`（傘 Issue）は直接の実装対象にしない。
- `type:human-gate` / `gate:human-required` が付いた Issue は、**人間の確認（デバイス検証・署名・UI レビュー等）を経るまで Done にしない**。
- **Codex 実行ポリシー**: `agent:codex-*` は候補（ルーティング）ラベルで Codex 実行許可ではない。Codex Cloud の起動（assign / delegate / `@Codex`）は人間の明示許可があるときのみで、Claude は行わない。正典は [`docs/linear-conventions.md`](./docs/linear-conventions.md) §2.1。

---

## 3. ビルド・テスト・検証

.NET 8 SDK が必要。

```sh
dotnet restore AiPhotoViewer.sln
dotnet build AiPhotoViewer.sln          # 全プロジェクトが警告/エラー 0 でビルドできること
dotnet test  AiPhotoViewer.sln          # スモークテストがパスすること
dotnet format AiPhotoViewer.sln         # 提出前に整形（CI でも検査）
```

PR を出す前にローカルで build / test が通ることを確認する。CI（`.github/workflows/ci.yml`）が同じ検査を行う。

---

## 4. レイヤ依存規約（`docs/architecture.md` 2章）

- `Core` は **他のどの層も参照しない**（純粋ドメイン）。
- `Infrastructure` / `Database` / `Imaging` / `AI` / `Jobs` / `Search` → `Core` **のみ** 参照。
- `UI` → `Core` と各機能層の **インターフェース** を参照（具象実装に依存しない）。
- `App` → `UI` / `Core` を参照し、DI で具象を結線する。
- UI と AI 推論を密結合させない。新しい推論・検索機能は必ずインターフェース経由で追加する。

---

## 5. ラベル ↔ ディレクトリ対応

`area:*` ラベルは `src/` のプロジェクトに対応する。担当領域はここで判断する。

| Linear ラベル | 主なディレクトリ / ファイル |
|---|---|
| `area:photo-ui` | `src/App/`, `src/UI/` |
| `area:image-core` | `src/Imaging/`（サムネイル・知覚ハッシュ） |
| `area:db` | `src/Database/`, `src/Core/Domain/` |
| `area:ai-runtime` | `src/AI/`（ONNX / 実行プロバイダ） |
| `area:search` | `src/Search/` |
| `area:faces` | `src/AI/`（顔検出/特徴量）, `src/Core/Domain/Faces.cs` |
| `area:jobs` | `src/Jobs/` |
| `area:privacy` | 横断（`docs/privacy.md` 準拠） |
| `area:base` | ソリューション / 基盤雛形 |

---

## 6. Phase（roadmap）↔ Stage（Linear）対応

| roadmap.md | Linear | 内容 |
|---|---|---|
| Phase 0 | P0 Technical validation | 技術検証 PoC（本書 `poc/`） |
| Phase 1 | Stage 1 core viewer | 高速ビューワー + DB + サムネイル |
| Phase 2 | Stage 2 search | 重複検知 + 類似画像検索 |
| Phase 3 | Stage 3 language and tags | 自然言語検索 + 自動タグ |
| Phase 4 | Stage 4 people grouping | 顔検出 + 人物グループ化（MVP 完成） |
| Phase 5〜7 | （MVP 後 / Stage 対象外） | OCR / 画質診断 / 補正・共有前チェック |

MVP は Phase 1〜4 = Stage 1〜4。MVP に後続機能を持ち込まない。

---

## 7. PoC の扱い

Phase 0 の PoC は **使い捨て前提**で、本実装（`src/`）とは分離して `poc/` 配下に置く。
ルール・測定値テンプレートは [`poc/README.md`](poc/README.md) を参照。PoC は `AiPhotoViewer.sln` に含めない。

---

## 8. コミット / PR 規約

- **コミットメッセージは日本語**、命令形・要点先頭（既存履歴に合わせる）。
- 1 Issue = 1 つの焦点を絞った PR（`type:implementation` の方針）。
- PR は最初 **ドラフト** で作成し、本文に対象 Issue（`DEV-xx`）を必ず記載。
- PR テンプレート（`.github/pull_request_template.md`）のチェックリストを満たす。

## 9. やってはいけないこと

- AI モデル本体（重みファイル）を commit しない（`models/*` は gitignore 済み。取得手順は `models/README.md`）。
- ローカル DB / キャッシュ / ログ（`*.db`, `thumbnails/`, `*.log`）を commit しない。
- `Core` から他層を参照する依存を足さない。
- MVP（Phase 1〜4）のスコープに後続フェーズの機能を混ぜない。
