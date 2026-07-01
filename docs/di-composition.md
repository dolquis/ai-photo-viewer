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
- **無効化方針**：モデル未取得時に該当機能を無効化する扱い。

本方針は PoC の実測値に依存しない。
ベクトル検索方式や既定の実行プロバイダといった PoC で確定する選択は、すべて契約 IF の背後に隠れる。
したがって本書は人間ゲート（DEV-318 Stage 1 readiness 判定）と独立に確定できる。

## 2. 合成ルートの所在

合成ルートは具象型を知る唯一の場所であり、`AiPhotoViewer.App` に置く。
`UI` 以下は各層のインターフェースのみを参照し、具象実装を参照しない。
この境界をコンパイル時の参照関係で担保するのが合成ルートの役割である。

ここで正典との整合に注意が必要になる。
`AGENTS.md` §4 と `docs/architecture.md` §2 はもともと「`App` → `UI` / `Core` を参照」と定めていたが、具象を登録するには合成ルートが具象の在処（`Infrastructure` / `Database` / `Imaging` / `AI` / `Jobs` / `Search`）を参照する必要がある。
合成ルートが全モジュールの具象を知ること自体は DI の標準的な構造であり、依存方向の逆転を一点に閉じ込めるための意図的な例外である。
この例外は lead 承認のもと正典へ明記した。`AGENTS.md` §4 と `docs/architecture.md` §2 は、`App` を合成ルートとして各機能層を参照してよい唯一の例外と定める。
したがって `App` は具象登録のため各機能層を参照する。`UI` 以下の参照規約（インターフェースのみ）は変えない。

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
無効化の主機構は、登録の差し替えではなく**呼び出し前のゲート**に置く。

現状の推論契約は `Task<float[]>` や `Task<IReadOnlyList<TagPrediction>>` のように素の値を返し、利用可否を伝えるステータス経路を持たない。
このため、モデルが無いときに値を返す Null 実装を既定にすると、実装者は例外を投げる（UI の非例外方針と矛盾する）か、空やプレースホルダを返す（正規の解析結果と誤認され永続化され得る）かのいずれかに追い込まれる。
したがって、**値を返す無効化実装を DI の既定にしない**。

方針は次のとおりとする。

- 機能ごとの利用可否を返す能力問い合わせ口（capability query）を**先に定義する**。合成ルートはモデルの所在（`IAppSettings.ModelDirectory` 配下）を確認し、各機能の可否をこの問い合わせ口へ反映する。
- UI はコマンドの可否をこの利用可否に束ねる。利用不可の機能はメニューやボタンを無効表示にし、そもそも呼び出さない。これにより「例外で操作を止めない」方針を、呼び出し自体を起こさないことで満たす。
- 解析パイプラインも同じ問い合わせ口を参照する。AI 解析はフォルダ取り込みや再解析で背景実行されるため（`architecture.md` §4、`mvp-spec.md` UC-1 / フロー A）、ゲートを UI コマンドだけに束ねると背景ジョブからモデル未取得サービスを呼び出してしまう。`IJobQueue` への投入とジョブ実行の前段でモデル依存ジョブ（`Embedding` / `Tagging` / `FaceDetection` / `FaceEmbedding` 等）の可否を確認し、利用不可の段はキュー投入せずスキップする。これは「解析済みならスキップ」する段階制御（`architecture.md` §4）と同じ場所に置く。
- フォールバック登録が必要な場合でも、捏造した解析結果を返さない。やむをえず登録する場合は、境界（ジョブ層）で扱える定義済みの「利用不可」例外を投げる形に限り、`architecture.md` §9 のエラー処理方針（境界でのみ捕捉）に従う。プレースホルダ値の永続化は禁じる。

能力問い合わせ口の具体形（契約 IF の置き場所・鍵・戻り値・更新契機）は §7 で確定する（DEV-388）。本節では「無効化は UI コマンドとジョブパイプラインの双方を同じ問い合わせ口でゲートし、無効化サービスに結果を捏造させない」という方針までを確定する。

## 7. 能力問い合わせ口（capability query）の契約

§6 で確定した「呼び出し前のゲート」を成立させる契約を、本節で確定する（DEV-388）。
UI コマンドの可否とジョブパイプラインの投入/実行前ゲートが、同一の問い合わせ口を参照する。
以下は設計の確定であり、コード化は後続の実装 Issue（`agent:codex-impl`）に委ねる。
本節のコード断片は契約の形を示す参考であり、`src/` への追加ではない。

### 7.1 契約の置き場所

能力問い合わせ口 `ICapabilityQuery` と、その鍵・戻り値の型は `Core` に置く。
UI（`Core` と各層 IF を参照）とジョブ（`Core` のみ参照）の双方が同一契約を参照できるのは、両者が共通して参照する `Core` に契約がある場合に限られる。
`Core` は他層を参照しない制約（`AGENTS.md` §4）を満たすため、鍵・戻り値も `Core` 内で完結する型で定義する。

鍵に既存の `AnalysisJobKind`（`AiPhotoViewer.Jobs`）を直接用いない。
`Core` は `Jobs` を参照できず、また `AnalysisJobKind` はモデル非依存の段（`Metadata` / `Thumbnail` / `FileHash` / `PerceptualHash`）を含むため、能力の鍵としては粒度が合わない。
モデル依存機能だけを列挙する `Core` の列挙 `AnalysisCapability` を新設し、これを鍵とする。

### 7.2 粒度と識別

能力の単位は「モデルの有無で可否が変わる推論機能」とする。
鍵は §4 で Singleton 登録するモデル依存サービス（`IImageEmbeddingService` / `ITaggingService` / `IFaceDetectionService` / `IFaceRecognitionService` / `IOcrService` / `IQualityAssessmentService` / `IUpscaleService` / `IDenoiseService`）を漏れなく覆う。
一部を欠くと、その UI コマンドが束ねる鍵を持たず、モデル未取得時に場当たりの例外/null 処理へ後退してしまう（§6 の「無効化サービスに結果を捏造させない」方針に反する）。

```csharp
namespace AiPhotoViewer.Core.Capabilities;

/// <summary>モデルの有無で可否が変わる推論機能。能力問い合わせの鍵。</summary>
public enum AnalysisCapability
{
    Embedding,          // 画像埋め込み（自然言語検索・類似検索の基盤）
    Tagging,            // 自動タグ
    FaceDetection,      // 顔検出
    FaceEmbedding,      // 顔特徴量
    Ocr,                // OCR（MVP後）
    QualityAssessment,  // 画質診断（MVP後）
    Upscale,            // アップスケール（MVP後・補正機能）
    Denoise,            // ノイズ除去（MVP後・補正機能）
}
```

鍵と消費側の対応づけは二通りある。

- 解析パイプラインの段（`Embedding` / `Tagging` / `FaceDetection` / `FaceEmbedding` / `Ocr` / `QualityAssessment`）は、`Jobs` 側が `AnalysisJobKind` → `AnalysisCapability` の対応表を持って背景ジョブをゲートする（`Jobs` は `Core` を参照するため対応表を `Jobs` に置ける）。モデル非依存の段（`Metadata` / `Thumbnail` / `FileHash` / `PerceptualHash`）は能力を持たず常に利用可とする。
- 補正機能（`Upscale` / `Denoise`）は解析パイプラインの段ではなく、`AnalysisJobKind` に対応を持たない。これらはユーザー操作で都度実行する非破壊処理であり、UI コマンドが直接この鍵で可否をゲートする。

いずれの場合も UI 側はコマンド → `AnalysisCapability` の対応を持つ。

### 7.3 戻り値の形

単純な可否ではなく、利用不可の理由を含める。
理由が無いと UI は「なぜ押せないか」を説明できず、ジョブ側もスキップ理由をログに残せない（`architecture.md` §9 は想定内ケースの握り潰しを禁じる）。

```csharp
namespace AiPhotoViewer.Core.Capabilities;

/// <summary>機能が利用できない理由。</summary>
public enum CapabilityUnavailableReason
{
    ModelNotFound,      // ModelDirectory 配下にモデルが無い
    DisabledBySetting,  // 設定で無効化されている（例: OcrEnabled=false）
    DeviceUnsupported,  // 実行プロバイダ/デバイスが未対応
    NotImplemented,     // MVP後フェーズで未提供
}

/// <summary>機能の利用可否と、不可の理由・解決見込みモデルの識別。</summary>
public sealed record CapabilityStatus(
    bool IsAvailable,
    CapabilityUnavailableReason? Reason = null,
    string? ModelName = null,
    string? ModelVersion = null);

/// <summary>機能ごとの利用可否を返す問い合わせ口。UI とジョブが共通で参照する。</summary>
public interface ICapabilityQuery
{
    CapabilityStatus GetStatus(AnalysisCapability capability);
    bool IsAvailable(AnalysisCapability capability);

    /// <summary>可否が変化したことの通知（設定変更・モデル取得/削除）。</summary>
    event EventHandler? CapabilitiesChanged;
}
```

`ModelDescriptor` との関係を次のとおり定める。
`ModelDescriptor`（`AiPhotoViewer.AI.Inference`）は AI 層の型であり、`Core` は AI を参照できないため `CapabilityStatus` に直接は持たせない。
利用可能時にどのモデルで解決するかを UI へ示す必要がある場合は、`Core` の素の文字列 `ModelName` / `ModelVersion` で表す。
これは `OcrResult` / `QualityScore`（`Core`）が既にモデル識別を `ModelName` / `ModelVersion` の文字列で保持しているのと同じ方針である。
`ModelDescriptor` を `Core` へ移して単一の型に統一する案は、契約の成立に必須ではないため実装 Issue の検討事項とする。

### 7.4 更新契機と通知

可否は次の契機で再評価する。

- `IAppSettings.ModelDirectory` の変更、および同ディレクトリ配下のモデルファイルの取得・削除。
- 機能トグルの変更（`FaceRecognitionEnabled` / `OcrEnabled` / `PrivacyCheckEnabled` / `BackgroundAnalysisEnabled`）。
- 実行プロバイダの利用可否・選択の変更（EP が `NotPresent` / `NotReady` から `EnsureReadyAsync` 等で `Ready` へ転じる、使用デバイスの切り替え。§7.6 (d) の入力）。

このうちモデルファイルの取得・削除はファイルシステム監視で観測できる。
一方、設定のインメモリ編集（`IAppSettings` のプロパティ変更）は観測経路を別に定める必要がある。
現状の `IAppSettings`（`src/Infrastructure/InfrastructureContracts.cs`）は可変プロパティと `SaveAsync` のみを公開し、変更通知を持たない。
このため能力実装が `IAppSettings` を受け取るだけでは、設定 UI での `ModelDirectory` 変更や機能トグルの変更を確定的に観測できず、`CanExecute` がモデルディレクトリのファイル変更か再起動まで陳腐化し得る。

観測経路を次のとおり定める。
設定変更の通知責務を `IAppSettings` に持たせる。
`IAppSettings` は可変設定の単一の源（§4）であり、変更通知を置く自然な場所はこの契約である。
具体的には `IAppSettings` に変更通知イベント（たとえば `event EventHandler? Changed;`）を追加し、プロパティ変更または `SaveAsync` の確定時に発火させる。
能力実装はこのイベントとファイルシステム監視の双方を購読し、いずれかの契機で可否を再評価する。
`IAppSettings` への変更通知の追加はこの契約の前提であり、後続の実装 Issue（`agent:codex-impl`）で `InfrastructureContracts.cs` に反映する。

実行プロバイダの利用可否・選択の変更も、モデルファイルや設定トグルのイベントとは独立に `DeviceUnsupported` を変え得る。
このため §7.6 (d) の `Core` 形プロバイダ状態入力にも変更通知を持たせ、能力実装がこれを購読して再評価する。
プロバイダ状態が通知を持たないと、EP の準備完了やデバイス切り替えの後も無関係な設定/モデル変更か再起動まで可否が陳腐化する。

再評価後、`ICapabilityQuery.CapabilitiesChanged` を発火する。
UI はこのイベントを購読し、コマンドの `CanExecute` を再評価する。
イベントはバックグラウンドスレッドで発火し得るため、UI への反映は §5 のとおり ViewModel が `Dispatcher` を介して UI スレッドへ戻す。
ジョブ側は投入・実行前のゲートには同期問い合わせ（`GetStatus` / `IsAvailable`）を用いるため、ゲート目的ではイベント購読を要しない。
ただし、いったんスキップした段の再キューには `CapabilitiesChanged` の購読が要る（§7.5）。

### 7.5 消費側の整合

UI とジョブは同一の `ICapabilityQuery` を参照する。

- UI: コマンドの `CanExecute` を `IsAvailable(capability)` に束ねる。利用不可の機能はメニュー/ボタンを無効表示にし、そもそも呼び出さない。
- ジョブ: `IJobQueue` への投入前とジョブ実行前の双方で、対象 `AnalysisJobKind` を `AnalysisCapability` に写して可否を確認する。利用不可の段はキュー投入せずスキップする（「解析済みならスキップ」する段階制御（`architecture.md` §4）と同じ場所に置く）。

両ゲートが同一契約を参照することで、背景ジョブがモデル未取得サービスを呼び出す事態を防ぐ（DEV-386 §6）。

利用不可のためにスキップした段は、能力が後で利用可へ転じたときに再キューする。
モデル未取得や機能無効のあいだに取り込んだ画像は、当該のモデル依存段（`Embedding` / `Tagging` / `FaceDetection` / `FaceEmbedding` 等）がスキップされ、`AnalysisStatus` が未解析のまま残る。
ゲートだけでは、後からモデルを取得・有効化しても UI の `CanExecute` が更新されるにとどまり、既出の画像に解析ジョブが積まれず手動の再解析まで解析されない。
これを避けるため、`CapabilitiesChanged` で能力が利用可へ転じた契機を購読する再キュー経路を設ける。
再キューはジョブ層（またはアプリケーション層のコーディネータ）が担い、当該段が未処理の既出画像に対応ジョブを投入する。
これは `architecture.md` §8 の「モデル変更時は該当解析を再キュー」および §4 の「解析済みならスキップ」と同じ場所・同じ冪等性の下で行う（重複や破損を生まない）。

この再キューは「当該段が未処理の画像」を確定的に選べることが前提となる。
現状のデータモデルは画像単位の単一 `Images.AnalysisStatus` しか持たず（`architecture.md` §6）、結果の不在は「スキップ」と「解析済みだが空結果（顔なし・タグなし）」を区別できない。
段ごとの状態を持たないと、後続の再キューは対象を取りこぼすか、空結果の解析済み画像を再処理してしまう。
したがって再キューの前提として、段ごとの完了状態（スキップ / 完了（空結果を含む）を判別できる段別ステータスまたはセンチネル）を持たせる。
段別状態の具体的なスキーマ（`Images.AnalysisStatus` の段別化、または段ごとの完了記録テーブル）は、解析パイプラインと DB の実装 Issue（`architecture.md` §4 / §6）の担当範囲とし、本書は「再キューは段別の完了状態に依存する」という要件を記録するにとどめる。

### 7.6 実装配置とフォールバック登録

契約は `Core` に置くが、既定実装は四つの入力を必要とする。
すなわち (a) 設定（`IAppSettings`、`Infrastructure`）、(b) `ModelDirectory` 配下のモデル有無（ファイルシステム）、(c) 能力ごとに必要なモデルの識別、(d) 実行プロバイダの利用可否（EP の `ReadyState`。`DeviceUnsupported` の判定に用い、`docs/windows-optimization.md` が本問い合わせ口に反映すると定める）である。
レイヤ規約上、単一の下位層からはこれらを同時に参照できない（`AI` は `Infrastructure` を参照せず、`Infrastructure` は `AI` を参照しない）。
したがって既定実装は `Infrastructure` に置き（`IAppSettings` とファイルシステムを参照）、能力 → 必要モデルの対応表（`Core` の型）は合成ルート（`App`）が注入する。

(d) の実行プロバイダの利用可否は、`Infrastructure` からは判定できない。
EP の `ReadyState` は AI/Windows の実行プロバイダカタログ（`ExecutionProviderCatalog`）に属する知識であり、`Infrastructure` がこれを参照するとレイヤ規約に反する。
したがってプロバイダ状態も `Core` 形の入力（たとえばプロバイダ状態のスナップショット、または `Core` に置くプロバイダ状態プローブ IF）として表し、実行プロバイダカタログを参照できる合成ルート（`App`）が組み立てて注入する。
既定実装はこの `Core` 形の入力から `DeviceUnsupported` を判定し、`AI`/`Windows` 層へ直接依存しない。
強制 EP や非対応 EP の局面でも、この入力によりゲートが実行不能なサービスの呼び出しを止められる。

(c) の対応表は、推論サービスを解決して得るのではなく、モデルマニフェスト/構成（`docs/model-management.md` のモデル定義）から静的に導く。
AI サービスの `ModelDescriptor` を実体から読むと、可否判定のために保護対象そのもの（モデル/セッションを所有する高コストな Singleton。§4）を先に構築してしまい、モデル未取得時は `ICapabilityQuery` が `ModelNotFound` を返す前に構築が失敗し得る。
これを避けるため、対応表はモデルをロードしない構成情報から組み立てる。
`ModelDescriptor` を実体から用いる場合でも、`Model` プロパティはモデルをロードせずに安全に読めることを実装の要件とする。
これにより AI 層の知識は `App` の合成時に構成として閉じ込められ、`Infrastructure` → `Core` のみの参照と `Core` の純粋性を保つ。
生存期間は Singleton とする（§4 の推論サービス・設定と同じく、可否の単一の源とするため）。

フォールバック登録の可否は次のとおり定める。
値を返す無効化実装を DI の既定にしない（§6）。
無効化は UI コマンドとジョブゲートで完結させ、これを主機構とする。
やむをえずフォールバックを登録する場合でも、捏造した解析結果を返さず、定義済みの「利用不可」例外を投げる形に限る（`architecture.md` §9）。
この例外はゲートを補完する多重防御であって、可否判定の主機構ではない。

例外の捕捉は、そのサービスを呼び出す境界で行う。
解析パイプラインの段（`Embedding` / `Tagging` 等）はジョブ層が境界となり、ジョブを `Failed` として次へ進む（`architecture.md` §9）。
一方、補正機能（`Upscale` / `Denoise`）は §7.2 のとおりジョブを介さず UI コマンドから直接実行するため、ジョブ層の境界が無い。
`CanExecute` と実行の間に可否が陳腐化する、あるいはモデルが削除される競合に備え、UI コマンドハンドラが同じ定義済み「利用不可」例外を捕捉し、エラー表示に集約する（例外で操作を止めない方針を保つ）。
すなわち「利用不可」例外は、ジョブ経由か UI 直接かにかかわらず、呼び出し側の境界で必ず捕捉する。

## 8. P0 段階の扱いと未決事項

本書は docs のみの変更であり、`src/` のコードと csproj を変更しない。
したがって `dotnet build AiPhotoViewer.sln` と `dotnet test` の結果に影響しない。
合成ルートのコード化、パッケージ参照の追加、無効化実装の用意は、いずれも実装 Issue（`agent:codex-impl`）の担当範囲とする。

**決定済み**: 合成ルートの参照境界は §2 のとおり確定した。`App` を「各機能層を参照してよい唯一の例外」とし、`AGENTS.md` §4 と `docs/architecture.md` §2 へ明記した（lead 承認済み）。

**決定済み**: 能力問い合わせ口（capability query）の契約は §7 のとおり確定した（DEV-388）。契約は `Core`（`ICapabilityQuery` / `AnalysisCapability` / `CapabilityStatus`）に置き、UI コマンドとジョブゲートが同一契約を参照する。既定実装は `Infrastructure` に置き、能力 → 必要モデルの対応表は `App` が注入する。フォールバックは定義済み「利用不可」例外に限る多重防御とし、値を返す無効化実装を既定にしない。

本書の確定後に引き継ぐ未決事項を挙げる。

- 接続ファクトリの具体形（単一接続の共有か、読み取り用と書き込み用の分離か）。PoC-3（DEV-44 SQLite 計測）の結果を踏まえて確定する。
- ベクトル索引の構築契機（起動時一括か増分か）と、`IVectorIndex` の初期実装（線形探索か HNSW か）。PoC-6（DEV-51）の結果に従う。

接続ファクトリとベクトル索引は契約 IF の背後の選択であり、本書の生存期間方針と解決経路を変えない。
§7 で契約 IF（`ICapabilityQuery` ほか）を確定したため、実装着手前に必要な設計ピースは揃った。コード化は後続の実装 Issue（`agent:codex-impl`）に委ねる。

---

## 参照

- `docs/architecture.md`：全体アーキテクチャ（§1 層構成、§3 データフロー、§5 ジョブ、§6 DB、§8 モデル管理）。
- `AGENTS.md` 4章：レイヤ依存規約。
- `src/App/Program.cs` / `src/App/App.axaml.cs`：合成ルートの導入点。
- `src/**/＊Contracts.cs`、`src/AI/Inference/InferenceServices.cs`、`src/Database/Repositories.cs`、`src/Jobs/JobQueue.cs`：登録対象の契約 IF。
