# DI 合成ルートと依存解決方針

本書は依存性注入（DI）の合成ルート（composition root）と、各契約インターフェースの登録方針を定義する。
全体設計は `docs/architecture.md`、レイヤ依存規約は `AGENTS.md` 4章を正典とし、本書はその DI 結線部分を具体化する。

本書は設計方針の確定を目的とし、具象実装やパッケージ追加を含まない。
実装は Stage 1 着手後に本書を契約として進める。

---

## 1. 目的とスコープ

`docs/architecture.md` §1 は「層間はインターフェースで接続し、実装は DI コンテナで注入する」と定め、§2 は「`App` → `UI` / `Core` を参照し、DI で具象実装を結線する」と定める。
しかし合成ルートの所在、各サービスの生存期間、UI への解決経路は未定義のままだった。
方針が無いまま Stage 1 を始めると、実装者ごとに結線方法が分かれ、レイヤ依存規約違反や生存期間に起因する不具合（DB 書き込みの競合、ジョブキューの多重生成など）を招きやすい。

本書が確定させるのは次の四点である。

- **合成ルートの所在**：どのプロジェクトのどの起動点で具象を登録し、画面を解決するか。
- **生存期間方針**：各契約 IF を Singleton と Transient のどちらで登録し、その根拠は何か。
- **解決経路**：UI（ViewModel）が具象へ依存せずに依存を受け取る流れ。
- **無効化方針**：モデル未取得時に該当機能を無効化する登録の扱い。

本方針は PoC の実測値に依存しない。
ベクトル検索方式や既定の実行プロバイダといった PoC で確定する選択は、すべて契約 IF の背後に隠れる。
したがって本書は人間ゲート（DEV-318 Stage 1 readiness 判定）と独立に確定できる。

## 2. 合成ルートの所在

合成ルートは具象型を知る唯一の場所であり、`AiPhotoViewer.App` に置く。
`App` だけが各機能層の csproj を参照し、`UI` 以下は具象実装を参照しない。
これはレイヤ依存規約（`UI` は各層のインターフェースのみ参照、`App` が DI で具象を結線する）をコンパイル時の参照関係で担保する。

起動シーケンスは次の順序とする。

1. `Program.Main` が Avalonia アプリを起動する（既存の `BuildAvaloniaApp` を維持する）。
2. `App.OnFrameworkInitializationCompleted` でサービス登録を実行し、`IServiceProvider` を構築する。
3. データベース初期化（`IDatabaseInitializer.InitializeAsync`）を起動時に一度だけ実行する。
4. `MainWindow` とその `DataContext`（シェル ViewModel）をコンテナから解決し、`desktop.MainWindow` に設定する。

現状の `App.axaml.cs` は `desktop.MainWindow = new MainWindow();` と直接生成している。
この一行が合成ルート導入時の置き換え対象になる。
具体的な置き換えは実装 Issue に委ね、本書では解決経路だけを定義する。

## 3. 採用するコンテナ

`Microsoft.Extensions.DependencyInjection`（以下 MS.DI）を採用する。
理由は三点である。

- .NET の標準であり、追加学習コストが小さく、`Microsoft.Extensions.Logging` や `Options` など周辺機能と素直に接続できる。
- コンストラクタ注入を基本とする本設計に必要な機能（生存期間管理、登録の差し替え）を過不足なく備える。
- Avalonia は DI コンテナを内蔵しないため外部コンテナを別途選ぶ必要があり、軽量な標準実装が初期の依存として妥当である。

Avalonia と MS.DI の接続は、`App` が構築した `IServiceProvider` を保持し、View 生成時にコンテナから解決する方式とする。
ViewLocator など Avalonia 側の生成機構を使う場合も、ViewModel の生成はコンテナ経由に統一する。

## 4. 生存期間方針

各契約 IF の生存期間を次の表で定める。
判断基準は「共有状態または高コスト資源を持つものは Singleton、状態を持たない軽量な変換は Transient」である。

| 契約 IF | プロジェクト | 生存期間 | 根拠 |
|---|---|---|---|
| `IAppLogger` | Infrastructure | Singleton | ログファイルとローテーションの単一所有者。全層で共有する。 |
| `IAppSettings` | Infrastructure | Singleton | 可変設定の単一の源。`SaveAsync` と監視フォルダ一覧を一貫させる。 |
| `ILibraryWatcher` | Infrastructure | Singleton | OS のファイル監視ハンドルと `Changed` イベントを所有する。 |
| `IDatabaseInitializer` | Database | Singleton | 起動時に一度だけ実行する初期化。状態を持たない。 |
| `IImageRepository` / `IEmbeddingRepository` | Database | Transient | 軽量な操作ラッパー。後述の単一書き込み機構の上で動く。 |
| `IThumbnailGenerator` | Imaging | Singleton | サムネイルキャッシュを共有し、生成の重複を避ける。 |
| `IPerceptualHasher` / `IFileHasher` | Imaging | Singleton | 状態を持たない。共有しても副作用が無く、生成コストを省ける。 |
| `IImageEmbeddingService` ほか推論サービス | AI | Singleton | モデル読み込みが高コスト。ONNX セッションを再利用する。 |
| `IVectorIndex` | Search | Singleton | 起動時に構築するインメモリ索引。`Add` / `Remove` が共有状態を変更する。 |
| `ISearchService` | Search | Singleton | 索引と推論サービスとリポジトリを束ねるファサード。 |
| `IJobQueue` | Jobs | Singleton | 単一のチャネルとワーカー群を持つ。`PendingCount` と `ProgressChanged` はアプリ全体で一意。 |
| シェル ViewModel | UI | Singleton | アプリ存続中ただ一つのメイン画面に対応する。 |
| 画面ごとの ViewModel | UI | Transient | 画面の生成ごとに新しい状態を持つ。 |

推論サービス（`IOcrService` / `IQualityAssessmentService` / `IUpscaleService` / `IDenoiseService`）は MVP 後フェーズの機能だが、登録方針は同じ Singleton とする。
MVP 段階では本書 §6 の無効化方針に従い、機能を無効化した実装を登録する。

リポジトリを Transient にする一方で、SQLite 接続そのものは単一の接続ファクトリ（Singleton）が供給する。
リポジトリは接続を都度借りる軽量ラッパーであり、接続の生存期間はファクトリ側が管理する。

## 5. 解決経路とスレッド方針

UI(ViewModel) は依存をコンストラクタ引数の契約 IF として受け取る。
ViewModel は具象型へダウンキャストせず、`IServiceProvider` を直接参照しない。
これにより `UI` プロジェクトは各層のインターフェースだけを参照し、具象は `App` の登録時にのみ結びつく。

設定画面の例では、ViewModel が `IAppSettings` を受け取り、`AddWatchedFolder` などの操作で値を変更する。
`IAppSettings` が書き込み操作を公開しているのは、UI が具象型へダウンキャストせずに設定を更新できるようにするためである（`InfrastructureContracts.cs` の設計意図）。

スレッドと生存期間に関する注意を次に挙げる。

- **DB 書き込みの直列化**：WAL モードで読み取りは並行させ、書き込みはデータ層の単一の書き込み機構（たとえば `SemaphoreSlim` による直列化）に集約する（`architecture.md` §6）。リポジトリを Transient にしても、書き込みの直列性はこの機構が保証する。
- **ジョブキューの単一性**：`IJobQueue` を Singleton にすることで、ワーカー数の設定と進捗通知をアプリ全体で一意に保つ。多重生成は二重処理と進捗の不整合を生むため避ける。
- **推論セッションの共有**：推論サービスを Singleton にし、ONNX セッションを再利用する。並行実行の度合いはジョブキューのワーカー数で制御し、推論側で無制限に並行化しない。
- **UI スレッドへの集約**：`ILibraryWatcher.Changed` や `IJobQueue.ProgressChanged` はバックグラウンドスレッドで発火する。これらを UI へ反映する際は ViewModel が Avalonia の `Dispatcher` を介して UI スレッドへ戻す。閲覧操作と解析は独立したパスを通り、解析が閲覧をブロックしない（`architecture.md` §3）。

## 6. モデル未取得時の無効化方針

`architecture.md` §5 は「モデル未ダウンロード時の該当機能無効化」を、§8 は解析結果への `ModelDescriptor` 記録を求める。
DI の登録時点でモデルの有無を判定し、無い場合は機能を無効化した実装を登録する。

方針は次のとおりとする。

- 合成ルートがモデルの所在（`IAppSettings.ModelDirectory` 配下）を確認し、対象モデルが揃う推論サービスだけを通常実装で登録する。
- モデルが無い機能には、呼び出しを安全に拒否する無効化実装（呼び出すと「利用不可」を返す Null 実装）を登録する。これにより呼び出し側は登録の有無を分岐せず、常に契約 IF を解決できる。
- UI はコマンドの可否を機能の利用可否に束ねる。利用不可の機能はメニューやボタンを無効表示にし、例外で操作を止めない。

利用可否を UI へ伝える経路（機能ごとの可否を返す問い合わせ口）の具体は実装 Issue で定める。
本書では「登録の差し替えで無効化を表現し、呼び出し側を分岐させない」という方針までを確定する。

## 7. P0 段階の扱いと未決事項

本書は docs のみの変更であり、`src/` のコードと csproj を変更しない。
したがって `dotnet build AiPhotoViewer.sln` と `dotnet test` の結果に影響しない。
合成ルートのコード化、パッケージ参照の追加、無効化実装の用意は、いずれも実装 Issue（`agent:codex-impl`）の担当範囲とする。

本書の確定後に実装 Issue へ引き継ぐ未決事項を挙げる。

- 接続ファクトリの具体形（単一接続の共有か、読み取り用と書き込み用の分離か）。PoC-3（DEV-44 SQLite 計測）の結果を踏まえて確定する。
- ベクトル索引の構築契機（起動時一括か増分か）と、`IVectorIndex` の初期実装（線形探索か HNSW か）。PoC-6（DEV-51）の結果に従う。
- 機能の利用可否を UI へ配信する問い合わせ口の形。

これらはいずれも契約 IF の背後の選択であり、本書の生存期間方針と解決経路を変えない。

---

## 参照

- `docs/architecture.md`：全体アーキテクチャ（§1 層構成、§3 データフロー、§5 ジョブ、§6 DB、§8 モデル管理）。
- `AGENTS.md` 4章：レイヤ依存規約。
- `src/App/Program.cs` / `src/App/App.axaml.cs`：合成ルートの導入点。
- `src/**/＊Contracts.cs`、`src/AI/Inference/InferenceServices.cs`、`src/Database/Repositories.cs`、`src/Jobs/JobQueue.cs`：登録対象の契約 IF。
