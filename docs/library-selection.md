# 外部ライブラリ選定レポート

本書は `docs/tech-selection.md` で確定した方針スタック（Avalonia / C#・.NET 8 /
ONNX Runtime / SQLite / HNSW）を、**具体的な NuGet パッケージ**へ落とし込むための
選定レポートである。各層のインターフェース契約（`src/**/*Contracts.cs` 等）を
どのパッケージで実装するかを対応づけ、導入順とライセンス上の判断点を示す。

前提:

- 現状（Phase 0）は雛形段階で、参照しているのは Avalonia 11.x とテスト系のみ。
  `src/` 各層はインターフェース契約だけが定義され、実装は未着手である。
- レイヤ依存規約（`AGENTS.md` 4章 / `docs/architecture.md` 2章）を厳守する。
  `Core` は他層を参照しない。各層は具象パッケージに直接依存せず、可能な限り
  `*.Abstractions` を参照して結線は `App`（DI 合成ルート）に集約する。
- ベクトル検索・推論ランタイムなど「PoC で方式を確定させる」と定めた領域は、
  本書でも「採用確定」ではなく「PoC の比較対象」として扱う。

---

## 1. 基盤（`App` / 全層）

| パッケージ | 用途 | 対応する契約・設計 |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection` | DI 合成ルート | `docs/di-composition.md` |
| `Microsoft.Extensions.Hosting` | 常駐ワーカーのホスト（`BackgroundService`） | `IJobQueue` のワーカー |

### 推奨: まず DI コンテナを導入する

現状は DI コンテナが無く、`App` が各層の具象を結線する設計（`docs/di-composition.md`）が
成立していない。`Microsoft.Extensions.DependencyInjection` を合成ルートに置き、
各機能層はインターフェースのみを公開する構成を最初に用意する。ジョブワーカーは
`Microsoft.Extensions.Hosting` の `BackgroundService` でホストすると、起動・停止・
キャンセルのライフサイクルを標準機構に委ねられる。

---

## 2. UI（`AiPhotoViewer.UI`）

| パッケージ | 用途 |
|---|---|
| `CommunityToolkit.Mvvm` | MVVM 基盤（`ObservableObject` / `RelayCommand` のソースジェネレータ） |

### 推奨: CommunityToolkit.Mvvm

`ViewModelBase` / `MainWindowViewModel` は現在手書きである。`[ObservableProperty]` /
`[RelayCommand]` のソースジェネレータで定型コードを削減でき、Avalonia の MVVM 開発で
事実上の標準となっている。ReactiveUI より軽量で、UI を推論実装から切り離す本設計と
相性が良い。

---

## 3. 画像処理（`AiPhotoViewer.Imaging`）

| パッケージ | 用途 | 対応する契約・PoC |
|---|---|---|
| `SixLabors.ImageSharp`（または `SkiaSharp`） | デコード・リサイズ・前処理 | `IThumbnailGenerator` / PoC-2 |
| `CoenM.ImageSharp.ImageHash` | pHash / dHash / aHash | `IPerceptualHasher` / PoC-4 |
| `MetadataExtractor` | EXIF（撮影日時・向き）読み取り | Metadata Reader（`architecture.md` 1章） |

### 推奨: マネージド画像ライブラリ + 知覚ハッシュ + メタデータ読取

- サムネイル生成・推論前処理は `SixLabors.ImageSharp` が本命。`IThumbnailGenerator`
  および PoC-2 に直結する。
- pHash/dHash は `CoenM.ImageSharp.ImageHash` が ImageSharp 上で実装でき、`IPerceptualHasher` と
  PoC-4 の重複検知に対応する。
- EXIF 読み取りは `MetadataExtractor` が .NET の標準的な選択肢で、設計の
  「Metadata Reader」コンポーネントに対応する。

### ライセンス上の判断点（要決定）

`SixLabors.ImageSharp` はバージョンでライセンスが異なる。**v2.x は Apache-2.0（無償）**、
**v3 以降は Six Labors Split License** で、OSS・小規模利用は無償だが
**商用配布では有償ライセンスが必要になる場合がある**。

注意すべきは、サムネイルを `SkiaSharp` に切り替えても **ImageSharp 依存は消えない**点である。
上で推奨した `CoenM.ImageSharp.ImageHash` は `SixLabors.ImageSharp`（`>= 2.1.3`）を
推移的依存に持つため、知覚ハッシュにこのパッケージを使う限り ImageSharp は依存グラフに残る。
したがって ImageSharp のライセンス判断は、サムネイル実装の選択とは独立に発生する。

整理すると、取り得る選択肢は次のとおり。

- **ImageSharp を v2.x（Apache-2.0）に固定して受け入れる**：`CoenM.ImageSharp.ImageHash`
  経由の ImageSharp を v2 系に解決すれば、商用配布でも有償ライセンスは不要。最も低コスト。
- **ImageSharp を完全に排除する**：サムネイルも知覚ハッシュも SkiaSharp 上で実装し、
  pHash/dHash を自前実装する（`CoenM.ImageSharp.ImageHash` を使わない）。依存は減るが
  実装コストが増える。
- **ImageSharp v3+ を採用する**：最新機能を使う代わりに、商用配布時はライセンス要否を確認する。

いずれにせよ `IThumbnailGenerator` / `IPerceptualHasher` の実装を差し替え可能に保ち、
配布形態が固まった段階で上記から選べる状態にしておく。

---

## 4. AI 推論（`AiPhotoViewer.AI`）

| パッケージ | 用途 | 対応する契約・PoC |
|---|---|---|
| `Microsoft.ML.OnnxRuntime` **または** `Microsoft.ML.OnnxRuntime.DirectML`（排他） | 推論ランタイム本体（CPU / DirectML） | `ExecutionProvider.Cpu` / `.DirectMl` / PoC-5 |
| `Microsoft.ML.OnnxRuntime.Extensions` | テキスト前処理（CLIP トークナイザ等） | `IImageEmbeddingService.EmbedTextAsync` / PoC-8 |

### 推奨: ネイティブパッケージは 1 つだけ選ぶ（CPU か DirectML）

ONNX Runtime のネイティブパッケージは**排他**である。`Microsoft.ML.OnnxRuntime.DirectML`
は **CPU EP を内包**しており、DirectML 版は CPU 版を置き換える。両方を同時に参照すると
managed/native バイナリやプロバイダ DLL が混在し、DirectML プロバイダの読み込みが
失敗しうる（バージョンも `Microsoft.ML.OnnxRuntime` 1.27.0 と
`Microsoft.ML.OnnxRuntime.DirectML` 1.24.4 のようにずれている）。

- **Windows（GPU 汎用を使う）**: `Microsoft.ML.OnnxRuntime.DirectML` のみを参照し、
  CPU は内包の CPU EP にフォールバックする。
- **CPU のみ / 非 Windows**: `Microsoft.ML.OnnxRuntime`（ベース）のみを参照する。
- RID（ターゲット）ごとにどちらか一方を選ぶ。同一ビルドで両方を参照しない。
  併用が避けられない場合はバージョンを厳密に一致させる。

`ExecutionProvider` enum（Cpu / DirectMl）の切替は、選んだネイティブパッケージが提供する
EP の範囲内でセッション生成時に指定する。TensorRT / OpenVINO は後続フェーズの任意最適化とする。

自然言語検索（PoC-8）で CLIP 系のテキスト埋め込みを使う場合、`Microsoft.ML.Tokenizers`
単体には現状 CLIP トークナイザが無いため、`Microsoft.ML.OnnxRuntime.Extensions` の
CLIP トークナイザを用いるのが現実的である。採用モデルは PoC-5 / PoC-8 の計測で確定する。

---

## 5. データベース（`AiPhotoViewer.Database`）

| パッケージ | 用途 | 対応する契約 |
|---|---|---|
| `Microsoft.Data.Sqlite` | SQLite アクセス（WAL） | `IImageRepository` / `IDatabaseInitializer` |
| `Dapper`（任意） | 手書き SQL の軽量マッピング | 各リポジトリ |

### 推奨: Microsoft.Data.Sqlite（+ 任意で Dapper）

`docs/tech-selection.md` の確定事項どおり `Microsoft.Data.Sqlite` を採用する。
クロスプラットフォームのネイティブ同梱には `SQLitePCLRaw.bundle_e_sqlite3` を併用する。
リポジトリの手書き SQL は `Dapper` で簡潔にマッピングできる。

### EF Core を採らない理由

「書き込みはリポジトリ層で直列化」「スキーマ／マイグレーションを `IDatabaseInitializer` で
自前管理」という設計（`docs/architecture.md` 6章）に対し、EF Core は抽象が重く過剰で、
WAL 直列化や後述の `sqlite-vec` 連携とも噛み合いにくい。Dapper + 手書きマイグレーションを
推奨する。

---

## 6. ベクトル検索（`AiPhotoViewer.Search`）

`IVectorIndex` の実装候補。`docs/poc-tasks.md` PoC-6 で方式を確定させる方針のため、
本書でも「導入確定」ではなく**比較対象**として扱う。

| 候補 | 状態・評価 |
|---|---|
| 線形探索（初期実装） | 追加依存なし。`IVectorIndex` を満たす最小実装。規模拡大まではこれで足りる |
| `sqlite-vec` | NuGet 提供は 0.1.x の alpha 段階。SQLite 統合は魅力的だが成熟度・配布管理を要検証 |
| HNSW 系（`HNSW.Net` 等） | 純マネージドだがメンテ状況にばらつき。PoC-6 で性能・メモリを実測して判断 |

### 推奨: 初期は線形探索、PoC-6 の計測で確定

契約どおりまず線形探索で動かし、10 万件規模の要件（検索 1 秒以内）に対して
HNSW か `sqlite-vec` を PoC-6 で比較して確定する。`IVectorIndex` を介するため、
実装の差し替えは後から可能である。

---

## 7. バックグラウンドジョブ（`AiPhotoViewer.Jobs`）

### 推奨: 追加ライブラリ不要

`System.Threading.Channels`（BCL 標準）で `IJobQueue` の設計（優先度・キャンセル・
一時停止/再開・進捗通知）は成立する。Hangfire / Quartz はサーバ常駐・永続スケジューリング
向けで、ローカル完結アプリには過剰なため採用しない。ワーカーの常駐は §1 の
`Microsoft.Extensions.Hosting` に委ねる。

---

## 8. テスト・計測（`tests/`）

| パッケージ | 用途 |
|---|---|
| `BenchmarkDotNet` | PoC-1〜8 の速度・メモリ計測 |
| `NSubstitute` | インターフェースのモック |
| `Shouldly`（任意） | アサーションの可読性向上 |

### 推奨: 計測基盤とモックを早期に整える

- **`BenchmarkDotNet`**：PoC-1〜8 はすべて速度・メモリの計測が合格条件であり
  （`docs/poc-tasks.md`）、使い捨て PoC（`poc/`）の測定基盤として最適。
- **`NSubstitute`**：全層がインターフェース駆動のため、各契約のテストでモックが効く。
  `Moq` は近年の同梱挙動の問題を避ける観点から採用しない。
- アサーションの可読性を上げたい場合は `Shouldly` を用いる。`FluentAssertions` は
  v8 以降が商用有償化したため、無償方針なら避ける。

---

## 9. 導入順（推奨）

1. `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting`
   （合成ルートの土台。これが無いと各層を結線できない）。
2. `CommunityToolkit.Mvvm`（UI 着手の前提。Phase 1）。
3. `Microsoft.Data.Sqlite`（+ `Dapper`）（PoC-3 / Phase 1）。
4. 画像系（`SixLabors.ImageSharp` または `SkiaSharp` / `CoenM.ImageSharp.ImageHash` /
   `MetadataExtractor`）（PoC-2・PoC-4 / Phase 1）。
5. `BenchmarkDotNet` + `NSubstitute`（Phase 0 の全 PoC の計測・検証）。
6. ONNX Runtime 系（`Microsoft.ML.OnnxRuntime` **または** `.DirectML` のいずれか一方 +
   `Microsoft.ML.OnnxRuntime.Extensions`）（PoC-5・PoC-8 / Phase 2 以降）。
7. ベクトル検索（PoC-6 の比較結果で確定）。

---

## 10. 採用一覧（まとめ）

| 層 / 契約 | パッケージ | 区分 |
|---|---|---|
| App（DI） | `Microsoft.Extensions.DependencyInjection` / `.Hosting` | 推奨・基盤 |
| UI | `CommunityToolkit.Mvvm` | 推奨 |
| Imaging | `SixLabors.ImageSharp`（または `SkiaSharp`） | 推奨・要ライセンス判断 |
| Imaging | `CoenM.ImageSharp.ImageHash` / `MetadataExtractor` | 推奨 |
| AI | `Microsoft.ML.OnnxRuntime` **か** `.DirectML`（排他）+ `.OnnxRuntime.Extensions` | 推奨（Phase 2〜） |
| Database | `Microsoft.Data.Sqlite`（+ `Dapper`） | 推奨 |
| Search | 線形探索 → `sqlite-vec` / HNSW 系 | PoC-6 で確定 |
| Jobs | （標準 `System.Threading.Channels`） | 追加不要 |
| Tests | `BenchmarkDotNet` / `NSubstitute` / `Shouldly` | 推奨 |

---

## 11. ライセンス・成熟度の注意点（要判断）

- **`SixLabors.ImageSharp`**：v2.x は Apache-2.0、v3 以降は Six Labors Split License で
  商用配布時は有償ライセンスの要否を確認する。なお `CoenM.ImageSharp.ImageHash` が
  ImageSharp（`>= 2.1.3`）を推移的依存に持つため、サムネイルを `SkiaSharp` にしても
  ImageSharp は依存グラフに残る。完全排除には知覚ハッシュも非 ImageSharp 実装にする必要が
  ある（詳細は §3 のライセンス節）。
- **`FluentAssertions`**：v8 以降が商用有償化。無償方針なら `Shouldly` を用いる。
- **`sqlite-vec`**：NuGet 提供は alpha 段階。本採用は PoC-6 で安定性・配布管理を確認してから
  判断する。
