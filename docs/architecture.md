# アプリケーション設計書

本書はオンデバイス AI 画像ビューワーの全体設計を定義する。技術選定の根拠は
`docs/tech-selection.md`、MVP 範囲は `docs/mvp-spec.md` を参照。

## 1. 全体アーキテクチャ

レイヤードアーキテクチャを採用し、UI と AI 推論を密結合させない。
依存方向は上位 → 下位の一方向、下位層は上位層を参照しない。

```text
UI Layer            画像ビューワー / サムネイルグリッド / 検索 UI /
                    AI解析結果表示 / 重複管理 / 人物管理 / 設定

Application Layer   ライブラリ管理 / 検索制御 / タグ管理 / 重複管理 /
                    人物管理 / AIジョブ管理

Domain Layer        ImageItem / ImageEmbedding / Tag / Face / Person /
                    OcrResult / QualityScore / DuplicateGroup

Infrastructure      File System Watcher / SQLite Repository /
Layer               Thumbnail Cache / Vector Index / AI Model Runtime /
                    Metadata Reader
```

層間はインターフェースで接続し、実装は DI コンテナで注入する。

## 2. モジュール構成（プロジェクト対応）

| プロジェクト | 層 | 責務 |
|---|---|---|
| `AiPhotoViewer.App` | UI | Avalonia アプリ起動、View、DI 構成 |
| `AiPhotoViewer.UI` | UI | ViewModel、UI ロジック（推論実装に非依存） |
| `AiPhotoViewer.Core` | Domain | ドメインモデル（引継ぎ文書 8章のレコード）、共通契約 |
| `AiPhotoViewer.Infrastructure` | Infrastructure | ファイル監視、設定、ロギング |
| `AiPhotoViewer.Database` | Infrastructure | SQLite リポジトリ、スキーマ管理 |
| `AiPhotoViewer.Imaging` | Infrastructure | サムネイル生成、知覚ハッシュ、ファイルハッシュ |
| `AiPhotoViewer.AI` | Infrastructure | 推論サービス抽象（`IImageEmbeddingService` 等）と ONNX 実装 |
| `AiPhotoViewer.Jobs` | Application | バックグラウンドジョブキュー、優先度制御 |
| `AiPhotoViewer.Search` | Application | ベクトル索引、自然言語/類似検索ファサード |
| `AiPhotoViewer.Tests` | - | ユニット/結合テスト |

参照規則:

- `Core` は他のどの層も参照しない。
- `Infrastructure` / `Database` / `Imaging` / `AI` / `Jobs` / `Search` → `Core` のみ参照。
- `UI` → `Core` および各機能層のインターフェースを参照（具象実装に非依存）。
- `App` → `UI` / `Core` を参照し、DI で具象実装を結線する。

DI の合成ルート（具象の登録場所・生存期間・解決経路）は `docs/di-composition.md` を参照。

## 3. データフロー

```text
[ユーザー操作] → UI(ViewModel)
                   │ コマンド
                   ▼
            Application Layer ── ジョブ投入 ──▶ Jobs(JobQueue)
                   │                                  │
                   │ 問い合わせ                        │ 解析実行
                   ▼                                  ▼
            Database(SQLite) ◀── 結果保存 ──── AI / Imaging / Search
                   │
                   └── 通知（INotifyPropertyChanged / イベント）──▶ UI 更新
```

閲覧操作（画像読み込み・スクロール）と AI 解析は独立したパスを通り、
解析が閲覧をブロックしない（引継ぎ文書 11.1）。

## 4. AI 処理パイプライン

画像追加時、引継ぎ文書 6.2 の順序で処理する。各段階はジョブキュー上の独立ジョブ。

```text
1. ファイル検出        6. DB 登録            11. OCR              ※MVP後
2. メタデータ読み取り   7. 画像埋め込み生成     12. 画質診断          ※MVP後
3. サムネイル生成      8. 自動タグ生成        13. 類似・重複グループ更新
4. 完全一致ハッシュ     9. 顔検出
5. pHash/dHash 生成   10. 顔特徴量生成
```

MVP は 1〜10 と 13 を対象。11〜12 は Phase 5〜6。
各段階は解析済みならスキップし、ファイル変更検知時に該当段階のみ再実行する。

## 5. バックグラウンドジョブ設計

`AiPhotoViewer.Jobs` の `IJobQueue` が中心（雛形: `src/Jobs/JobQueue.cs`）。

- 実装は .NET の `System.Threading.Channels` ベース。ワーカー数は設定可能。
- 優先度: `JobPriority`（High/Normal/Low）。閲覧中フォルダの画像を High に昇格。
- ジョブ種別: `AnalysisJobKind`（Metadata〜QualityAssessment）。
- 機能要件: キャンセル（`CancellationToken`）、一時停止/再開、進捗通知
  （`ProgressChanged` イベント）、解析済みスキップ、バッテリー駆動時の低負荷モード、
  GPU 使用率制限、モデル未ダウンロード時の該当機能無効化。
- 冪等性: ジョブは再実行可能に設計し、途中終了後の再開で重複や破損を生まない。

## 6. データベース設計

SQLite。スキーマは引継ぎ文書 8章に対応（`src/Core/Domain/` のレコードと 1:1）。

主テーブル: `Images`, `ImageEmbeddings`, `Tags`, `ImageTags`, `Faces`, `Persons`,
`OcrResults`, `QualityScores`, `DuplicateGroups`, `DuplicateGroupItems`, `Settings`。

設計方針:

- WAL モードで読み取り並行性を確保。書き込みはリポジトリ層で直列化。
- インデックス: `Images.FilePath`(UNIQUE), `Images.FileHash`, `Images.PerceptualHash`,
  `Images.AnalysisStatus`, `ImageTags.ImageId/TagId`, `Faces.ImageId/PersonId`。
- 埋め込みベクトルは `ImageEmbeddings.VectorBlob` に格納。`ModelName`/`ModelVersion`
  を必須カラムとし、モデル変更時の再解析対象を特定可能にする。
- スキーマバージョンを `Settings` に保持し、起動時にマイグレーションを適用
  （`IDatabaseInitializer`）。
- 10 万枚規模を想定し、一覧取得はページング、サムネイルは遅延読み込み。

## 7. ベクトル検索設計

- 埋め込み本体は SQLite に永続化（単一ソース・オブ・トゥルース）。
- 起動時／増分で HNSW インメモリ索引（`IVectorIndex`）を構築。
- 自然言語検索: クエリ文をテキスト埋め込み化 → 索引で近傍 k 件 → スコア順表示。
- 類似画像検索: 対象画像の埋め込みで同様に検索。
- 初期実装は線形探索でも可。規模拡大時に HNSW / `sqlite-vec` へ差し替え（`docs/poc-tasks.md`）。

## 8. モデル管理設計

詳細は `docs/model-management.md`。要点:

- モデルはアプリ本体と分離し、`models/` 配下にバージョン別に配置。
- 各推論サービスは `ModelDescriptor`（名前・バージョン）を公開し、解析結果に必ず記録。
- 軽量/重量モデルを設定で切替可能。モデル変更時は該当解析を再キュー。

## 9. エラー処理方針

- 境界（ファイル I/O・モデル読み込み・DB）でのみ例外を捕捉し、内部ロジックは
  事前条件を信頼する。
- 画像 1 枚の解析失敗は当該ジョブを `Failed` にして次へ進む（バッチ全体を止めない）。
- 失敗は AI 解析画面のエラー一覧に集約表示し、ユーザーが再試行できる。
- 破損ファイル・未対応形式・権限エラーは想定内ケースとして握りつぶさずログ記録。

## 10. 設定管理

- `IAppSettings`（`src/Infrastructure/InfrastructureContracts.cs`）が設定の読み書きを担う。
- 保存先はユーザープロファイル配下の JSON（DB とは分離し、可搬・可読に保つ）。
- 項目: 監視フォルダ、モデル保存場所、使用デバイス、背景解析有効/無効、
  バッテリー時停止、顔認識/OCR/プライバシーチェックの有効/無効、DB/キャッシュ再構築。

## 11. ログ設計

- `IAppLogger` 抽象。出力はローカルファイルのみ。外部送信しない。
- レベル: Info / Warn / Error。ログにはパス等は記録するが顔特徴量・OCR 全文は記録しない。
- ローテーション（サイズ/日数）を設け、肥大化を防ぐ。
- 設定画面からログフォルダを開く・消去できる。
