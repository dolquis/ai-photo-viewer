# 技術選定レポート

本書は Windows 向けオンデバイス AI 画像ビューワーの技術スタックを比較・選定する。
前提条件（引継ぎ文書 15章）は以下のとおり。

- Windows 向けデスクトップアプリ
- ネットワーク非接続で主要 AI 機能が動作
- 画像・顔特徴量・OCR 結果を外部送信しない
- AI モデルはローカル実行
- 高速な閲覧体験
- AI 解析はバックグラウンドジョブで非同期実行
- 非破壊編集
- 将来 10 万枚規模に耐える設計

---

## 1. UI フレームワーク

| 観点 | WinUI 3 | WPF | Avalonia UI |
|---|---|---|---|
| Windows ネイティブ感 | ◎ 最新 Fluent | ○ 旧来 Win32 系 | ○ 独自描画で近似 |
| 高 DPI 対応 | ◎ | ○ | ◎ |
| 画像表示性能 | ◎（Composition/SwapChain） | ○ | ◎（Skia, GPU 合成） |
| 仮想化リスト/グリッド | ○（ItemsRepeater） | ◎（成熟） | ○（仮想化対応、要設計） |
| C#/.NET との相性 | ◎ | ◎ | ◎ |
| 長期保守性 | △ 変遷が激しい | ◎ 安定・枯れている | ○ 活発・後方互換に配慮 |
| 開発速度 | ○ | ◎ 情報量が多い | ○ |
| 配布しやすさ | △ MSIX 前提が強い | ◎ self-contained 容易 | ◎ self-contained 容易 |
| クロスプラットフォーム | × Windows 専用 | × Windows 専用 | ◎ Win/macOS/Linux |

### 推奨: Avalonia UI

理由:

- README が "Windows-first, cross-platform core" を掲げており、Core/AI/Database
  などの基盤層を .NET 共通ライブラリに保つ本設計と最も整合する。
- Skia ベースの GPU 合成描画はサムネイルグリッド・大画像表示で十分な性能を持つ。
- self-contained 配布が容易で、MSIX に縛られず初期配布の自由度が高い。
- 将来モバイル/macOS を非目標から外す判断をした場合も、UI 資産を流用できる。

次点は **WinUI 3**（Windows ネイティブ感を最優先する場合）。WPF は最も枯れているが、
クロスプラットフォーム核という方針と相反するため非推奨。

トレードオフ: Avalonia はリスト/グリッド仮想化が WPF ほど「黙って速い」わけではなく、
サムネイルグリッドの仮想化は Phase 1 で明示的に検証・設計する必要がある
（`docs/poc-tasks.md` PoC-1 参照）。

---

## 2. 実装言語

| 観点 | C#/.NET | C++ | Rust |
|---|---|---|---|
| UI 開発効率 | ◎ | △ | △ |
| エコシステム（画像/ML） | ◎ ONNX Runtime, ImageSharp 等 | ◎ | ○ 発展途上 |
| メモリ安全性 | ○（GC） | × | ◎ |
| 推論ホットパス性能 | ○ | ◎ | ◎ |
| 学習・保守コスト | 低 | 高 | 中〜高 |

### 推奨: C#/.NET 8（UI・アプリケーション全層）

- UI フレームワーク（Avalonia）と同一言語で開発効率・保守性が最も高い。
- ONNX Runtime に公式 .NET バインディングがあり、推論基盤を C# で完結できる。
- 画像処理・推論前処理のホットパスで性能が不足する場合に限り、Phase 0 の計測結果に基づき
  C++/Rust ネイティブライブラリを P/Invoke で部分導入する（早すぎる最適化を避ける）。
- Python はプロトタイプ／モデル変換用途に限定し、製品コードには含めない。

---

## 3. AI 推論ランタイム

| 観点 | ONNX Runtime | Windows ML | DirectML | OpenVINO | TensorRT |
|---|---|---|---|---|---|
| ベンダー非依存 | ◎ | ○ | ◎（Win GPU 汎用） | △ Intel 寄り | × NVIDIA 専用 |
| .NET バインディング | ◎ 公式 | ○ | （ORT 経由） | ○ | △ |
| CPU フォールバック | ◎ | ◎ | ×（GPU 前提） | ◎ | × |
| NPU 対応 | ○（EP 経由） | ◎ | △ | ◎ Intel NPU | × |
| 配布の容易さ | ◎ NuGet | ○ OS 同梱 | ○ | △ | △ |

### 推奨: Windows ML（ONNX Runtime）を基盤に実行プロバイダ（EP）を抽象化

- 共通基盤は ONNX Runtime とし、Windows 向けの配布経路として Windows ML（Windows が保守する ONNX Runtime）を採る。API は ORT と同一で、`ExecutionProviderCatalog` が EP の取得と登録を担う。
- `ExecutionProvider`（CPU / DirectML / NPU）の抽象は維持する（雛形: `src/AI/Inference/ExecutionProvider.cs`）。Windows ML は EP を自動選択するため、この列挙は利用者の優先指定（上書き）として機能させる。
- 既定は Windows ML による自動 EP 選択とし、NPU/GPU を優先して CPU へフォールバックする。これにより GPU/NPU の無い環境でも CPU で全機能が動作する（受け入れ条件 AC-8）。GPU は DirectML EP で Intel/AMD/NVIDIA を問わず活用し、CPU と DirectML は Windows ML ランタイムに同梱される。
- NPU EP（QNN / OpenVINO など）は `ExecutionProviderCatalog` から取得するか、オフライン要件のため自前で同梱する（bring your own EPs）。TensorRT 系は後続フェーズの任意最適化扱い。
- DirectML は単独利用が保守モード（sustained engineering）に移り、新機能は Windows ML 経由の ORT デプロイへ移った。Windows 特化の具体策は `docs/windows-optimization.md` を参照。

---

## 4. データベースとベクトル検索

### データベース: SQLite（確定）

- 単一ファイル・ゼロ構成・組み込みで、ローカル完結アプリに最適。
- `Microsoft.Data.Sqlite` を .NET から利用。WAL モードで読み取り並行性を確保。
- 保存対象は引継ぎ文書 8章のデータモデル（画像メタデータ、ハッシュ、タグ、顔、
  OCR、品質スコア、重複グループ、設定）。

### ベクトル検索方式の比較

| 方式 | 長所 | 短所 |
|---|---|---|
| SQLite BLOB に格納し全件線形探索 | 実装が単純、追加依存なし | 10 万枚規模で検索が遅い |
| `sqlite-vec` 拡張 | SQLite に統合、SQL で完結 | ネイティブ拡張の配布管理が必要 |
| HNSW インメモリ索引（例: HNSW.Net 系） | 近似最近傍が高速、10 万枚規模で実用的 | 索引の永続化・再構築を自前管理 |
| FAISS | 高性能・実績豊富 | ネイティブ依存が重く Windows 配布が煩雑 |

### 推奨: HNSW インメモリ索引 + SQLite 永続化のハイブリッド

- 埋め込みベクトル本体は SQLite に BLOB 保存（永続化・バックアップの単一ソース）。
- 起動時または増分で HNSW インメモリ索引を構築し、近似最近傍検索を高速化
  （引継ぎ文書 11.3「検索 1 秒以内」目標を満たす）。
- 索引抽象は `src/Search/SearchContracts.cs` の `IVectorIndex` で表現済み。
  実装を差し替え可能にし、初期は線形探索、規模拡大時に HNSW へ移行する余地を残す。
- `sqlite-vec` は有力な代替で、Phase 2 の PoC（PoC-6）で HNSW と比較評価する。

---

## 5. 最終推奨スタック

| 領域 | 採用技術 |
|---|---|
| UI | Avalonia UI 11.x（MVVM） |
| 言語 | C#/.NET 8（ホットパスのみ将来ネイティブ併用を検討） |
| AI 推論 | Windows ML（ONNX Runtime）。CPU / DirectML EP を既定とし、NPU EP は取得または同梱 |
| DB | SQLite（`Microsoft.Data.Sqlite`, WAL） |
| ベクトル検索 | SQLite BLOB 永続化 + HNSW インメモリ索引 |
| 画像処理 | マネージドライブラリ中心、性能要件次第でネイティブ併用 |
| バックグラウンド処理 | .NET の `Channel` ベースのジョブキュー（`IJobQueue`） |

この構成は「完全ローカル動作」「ベンダー非依存」「交換可能なモデル設計」
「クロスプラットフォーム核」というプロジェクト方針をすべて満たす。

本書の方針スタックを具体的な NuGet パッケージへ落とし込んだ選定は
[`docs/library-selection.md`](library-selection.md) を参照。
