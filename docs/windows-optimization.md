# Windows ネイティブ最適化を非 UI 層で行う技術調査

本書は「Windows 特化による高速化を、UI フレームワークを変えずに非 UI 層で実現する」方針の具体策をまとめる。
対象は推論ランタイム、画像デコードとサムネイル生成、データベースとベクトル検索、ファイル監視と電源管理、そして Windows API を非 UI 層から呼ぶための基盤技術である。

前提となる評価（UI フレームワークは Avalonia を維持し、Windows 特化は非 UI 層で行う）は別途合意済みとする。
技術選定の正典は `docs/tech-selection.md`、全体設計は `docs/architecture.md`、DI 結線は `docs/di-composition.md` を参照する。

---

## 1. 本書の目的と適用範囲

本書が確定させるのは、各非 UI 層で採用する Windows ネイティブ技術と、その導入がプロジェクト構成（ターゲットフレームワーク、参照関係、CI、配布）に与える影響である。

本書が決めないことを先に明示する。

- 具体的な AI モデルの最終選定（埋め込み/タグ/顔）は Phase 0 の PoC で確定する（`docs/poc-tasks.md` PoC-5/7/8）。本書は候補の方向づけにとどめる。
- ベクトル索引の方式（HNSW と `sqlite-vec` の比較）は PoC-6 で確定する。本書は判断材料を整理する。
- 実装着手やパッケージ追加は実装 Issue の担当範囲とする。本書は契約と方針を定める設計文書である。

性能の所在を押さえることが本方針の出発点になる。
このアプリの性能を律速するのは画像デコード、サムネイル生成、推論、DB 問い合わせ、ベクトル検索であり、いずれも UI フレームワークから独立した非 UI 層に属する。
したがって高速化の投資先も非 UI 層に置く。

---

## 2. 基本原則：加速の実装を契約インターフェースの裏に閉じ込める

Windows ネイティブ技術の導入で `Core` の純粋性と層構造を崩さないために、三つの原則を置く。

- **`Core` は移植可能なまま保つ**：`net8.0` を維持し、Windows API を参照しない。ドメインモデルと契約だけを持ち、Linux 上でもビルド/テストできる状態を崩さない（`AGENTS.md` §4）。
- **加速は契約インターフェースの実装として足す**：`IThumbnailGenerator` や `IImageEmbeddingService` などの既存インターフェースは変えず、その背後に Windows 実装を差し込む。UI とジョブはインターフェースだけに依存し、加速手段の差し替えに影響されない。
- **Windows 依存を leaf の実装プロジェクトに隔離する**：Windows API を呼ぶコードは専用の実装プロジェクト（後述の `*.Windows`）に閉じ込め、合成ルート（`App`）からのみ参照する。これにより Windows ターゲットフレームワークの伝播を最小化する。

この三原則は、UI フレームワーク（Avalonia）を薄いシェルとして隔離した現状のレイヤ設計を、推論や画像処理の加速にもそのまま適用するものである。

---

## 3. Windows API を非 UI 層から呼ぶ基盤

WinUI 3 を使わなくても、Windows ランタイム（WinRT）API と Win32 API は通常の .NET アプリから呼べる。
ここでの選択がプロジェクト構成全体に波及するため、最初に基盤を固める。

### 3.1 二つの相互運用手段

- **C#/WinRT（CsWinRT）**：ターゲットフレームワークを `net8.0-windows10.0.19041.0` のように Windows SDK バージョン付きにすると、WinRT 型を呼べる。対象は `Windows.Graphics.Imaging` や `Windows.Storage`、`Windows.System.Power` などである。WinRT 投影アセンブリが自動で参照に加わる。制約として、未パッケージ（unpackaged）アプリから呼べるのは Windows 標準の WinRT 型に限り、サードパーティ製のカスタム WinRT 型は呼べない（パッケージ identity が要る）。本アプリが使うのは Windows 標準型のみなので、この制約には抵触しない。
- **C#/Win32（CsWin32）**：`Microsoft.Windows.CsWin32` は Win32 P/Invoke 署名を生成するソースジェネレータである。`GetSystemPowerStatus` のような Win32 関数を、手書きの DllImport なしで安全に呼べる。WinRT を介さない軽量な相互運用に向く。

### 3.2 ターゲットフレームワークとプロジェクト構成

WinRT 型を使うプロジェクトは `net8.0-windows10.0.x` を持つ必要がある。
ここで参照互換性に注意が要る。
純粋な `net8.0` プロジェクトは `net8.0-windows10.0.x` プロジェクトを参照できない。
そのため、機能層をそのまま Windows ターゲットに変えると、それを参照する `UI` や `App` まで連鎖する。

連鎖を避けるため、インターフェースと Windows 実装を別プロジェクトに分ける構成を採る。

- 各機能層（`Imaging` / `AI` / `Infrastructure`）は `net8.0` のまま、インターフェースと移植可能なマネージド既定実装を持つ。
- Windows ネイティブ実装は `AiPhotoViewer.Imaging.Windows` のような `net8.0-windows10.0.x` の実装プロジェクトに置く。
- これらの Windows 実装プロジェクトは合成ルート（`App`）だけが参照する。`App` は各機能層の具象を登録する唯一の例外として既に位置づけられている（`docs/di-composition.md` §2）ため、参照規約に新たな違反を生まない。
- `UI` は従来どおり `net8.0` のままインターフェースだけを参照する。

各プロジェクトのターゲットフレームワーク方針を次に示す。

| プロジェクト | ターゲット | Windows ネイティブ実装の置き場所 |
|---|---|---|
| `Core` | `net8.0` | なし（純粋ドメイン） |
| `UI` | `net8.0` | なし（インターフェース参照のみ） |
| `Database` | `net8.0` | なし（SQLite は移植可能） |
| `Search` | `net8.0` | なし（索引は移植可能） |
| `Jobs` | `net8.0` | なし（電源状態は IF 経由で受ける） |
| `Imaging` | `net8.0` | インターフェース＋マネージド既定実装 |
| `AI` | `net8.0` | インターフェース＋マネージド既定実装 |
| `Infrastructure` | `net8.0` | インターフェース＋マネージド既定実装 |
| `Imaging.Windows` | `net8.0-windows10.0.x` | WIC/Storage サムネイル実装 |
| `AI.Windows` | `net8.0-windows10.0.x` | Windows ML 推論実装 |
| `Infrastructure.Windows` | `net8.0-windows10.0.x` | 電源状態/高度なファイル監視 |
| `App` | `net8.0-windows10.0.x` | 合成ルート（既存 WinExe） |

マネージド既定実装を各機能層に残す理由は二つある。
第一に、`Core` だけでなく機能層のロジックもできるだけ Linux 上でテストできる状態を保てる。
第二に、Windows ネイティブ実装が使えない局面（コーデック未導入など）でフォールバックを提供でき、`docs/di-composition.md` §6 の無効化方針と組み合わせやすい。

別プロジェクトを増やすのが重い場合の代替として、機能層を `net8.0;net8.0-windows10.0.x` のマルチターゲットにし、`#if WINDOWS` で Windows コードを切り分ける方法もある。
この方法はプロジェクト数を増やさないが、条件コンパイルが増え、CI は Windows 上でも各ターゲットをビルドする必要がある。
本書は構成の見通しを優先し、実装プロジェクト分離を主案、マルチターゲットを代替案とする。

---

## 4. AI 推論：Windows ML と ONNX Runtime

### 4.1 Windows ML を基盤にする

2026 年時点で、Windows 向けのオンデバイス推論は **Windows ML** を基盤にするのが妥当である。
Windows ML は Windows が保守する ONNX Runtime（ORT）であり、API は ORT と同一で、ハードウェアに応じて実行プロバイダ（EP）を動的に選ぶ。
従来の DirectML は単独利用が保守モード（sustained engineering）に移り、新機能は Windows ML 経由の ORT デプロイへ移った。

この変化は `docs/tech-selection.md` §3 の前提（ONNX Runtime + DirectML EP）の更新を要する。
推論基盤を ORT に置く判断は保ったまま、Windows 向けの既定経路を Windows ML に更新する。
ただし `Core` から見た抽象（`IImageEmbeddingService` などの契約）は変わらない。

### 4.2 実行プロバイダの取得と登録

Windows ML は EP の発見/取得/登録を `ExecutionProviderCatalog` で提供する。

```csharp
// すべての互換 EP をダウンロード・登録する最短経路
var catalog = Microsoft.Windows.AI.MachineLearning.ExecutionProviderCatalog.GetDefault();
await catalog.EnsureAndRegisterCertifiedAsync();
// 以降は Microsoft.ML.OnnxRuntime の API でそのまま推論する
```

細かな制御が要る場合は、`FindAllProviders()` で各 EP の `ReadyState` を調べ、`EnsureReadyAsync()` でダウンロード/依存追加し、`TryRegister()` で ORT へ登録する。
`ReadyState` は `NotPresent`（未インストール）/ `NotReady`（インストール済みだが未登録）/ `Ready`（登録可能）を表す。

### 4.3 実行プロバイダとシリコンの対応

| EP | 対象シリコン | 配布 |
|---|---|---|
| CPU | すべて（フォールバック） | Windows ML ランタイムに同梱 |
| DirectML | DirectX 12 互換 GPU（Intel/AMD/NVIDIA） | Windows ML ランタイムに同梱 |
| QNN | Qualcomm Hexagon NPU（Snapdragon X 系） | カタログから動的取得、または自前同梱 |
| OpenVINO | Intel CPU/GPU/NPU（Core Ultra 系） | カタログから動的取得、または自前同梱 |
| NvTensorRtRtx | NVIDIA RTX GPU | カタログから動的取得、または自前同梱 |

CPU と GPU（DirectML）は Windows ML ランタイム（合計およそ 41 MB）に含まれ、追加取得なしで動く。
NPU 向けのハードウェア最適化 EP は Windows 11 24H2（ビルド 26100）以降が必要で、ベンダー EP はランタイム本体には含まれない。

本アプリの `src/AI/Inference/ExecutionProvider.cs` は `Cpu` / `DirectMl` / `Npu` の列挙を既に持つ。
この列挙は、Windows ML の自動選択に対する利用者の優先指定（明示的な上書き）として機能させる。
既定は自動選択に委ね、設定で特定 EP を強制できるようにする。

### 4.4 モデルの最適化

NPU や GPU で効率よく動かすには、ONNX への変換時に量子化（QDQ 形式など）を施すと効果が出やすい。
Olive や Foundry の変換ツールで QDQ ONNX を生成し、EP ごとの適合を PoC-5 で計測する。
解析結果には常にモデル名とバージョンを記録する方針（`docs/architecture.md` §8）は変えない。

---

## 5. 画像デコードとサムネイル生成

サムネイルグリッドの体感速度（`docs/mvp-spec.md` AC-1）は描画エンジンより画像デコードとキャッシュ戦略で決まる。
Windows には、デコードと縮小を効率化するネイティブ手段が複数ある。

### 5.1 Explorer のサムネイルキャッシュを再利用する

`StorageFile.GetThumbnailAsync(ThumbnailMode, requestedSize, ThumbnailOptions)` は、Windows のサムネイルディスクキャッシュからサムネイルを返す。
このキャッシュはエクスプローラーが使うものと同じである。
既に OS がキャッシュ済みの画像なら、自前のデコードなしで即座にサムネイルを得られる。
`ThumbnailOptions.ReturnOnlyIfCached` を使えば、キャッシュ未生成時のコストを避けて段階的に埋める設計もできる。

注意点として、`GetThumbnailAsync` はディスクキャッシュの上限サイズに従う。
キャッシュ上限を超える大きめのサムネイルが要る場面では `GetScaledImageAsThumbnailAsync` が使えるが、こちらはディスクキャッシュを使わないため、品質と引き換えに生成コストが上がる。

### 5.2 WIC で縮小デコードする

`Windows.Graphics.Imaging.BitmapDecoder` は WIC（Windows Imaging Component）を背後に持つ。
全画素を展開してから縮小するのではなく、デコード時に縮小（`BitmapTransform` による scaled decode）できるため、メモリと時間を抑えられる。
EXIF の回転フラグはサムネイル取得時に自動適用され、埋め込みサムネイル（JPEG/TIFF が持つ小サイズ画像）も `GetThumbnailAsync` で取り出せる。

### 5.3 高品質サムネイル生成に MagicScaler を使う

`PhotoSauce.MagicScaler` は WIC パイプラインに統合された .NET の画像処理ライブラリで、MIT ライセンスである。
WIC の高速性を活かしつつ、独自のリサンプリングで WIC 単体より高品質に縮小する。
一行ずつ処理してメモリを数百 KB に抑える、JPEG を DCT ドメインで縮小/回転する、線形光空間でリサンプリングする、といった最適化を持つ。
中間サイズまで WIC で高速に縮小し、最終サイズを高品質に仕上げるハイブリッド縮小も備える。

これらの特性から、MagicScaler は `IThumbnailGenerator` の Windows 既定実装の有力候補になる。

### 5.4 対応形式の拡張

WIC は OS のコーデックを通じて HEIF/AVIF/RAW にも対応できる。
これらは MVP の対象外（`docs/mvp-spec.md` §1）だが、`IThumbnailGenerator` の裏を WIC にしておけば、将来の形式拡張を OS コーデック側に委ねられる。

### 5.5 マネージドフォールバック

Windows コーデックが使えない環境やテスト用に、マネージドのデコード/縮小経路を残す。
速度重視なら SkiaSharp、MagicScaler のマネージドパイプラインも選べる。
このフォールバックは §2 の「機能層にマネージド既定実装を残す」方針に対応する。

| 用途 | Windows 実装 | マネージドフォールバック |
|---|---|---|
| 一覧の遅延サムネイル | StorageFile キャッシュ → WIC/MagicScaler | SkiaSharp/MagicScaler（マネージド） |
| ビューアの拡大表示 | WIC 縮小デコード | SkiaSharp |
| ハッシュ用デコード | WIC | SkiaSharp/ImageSharp |

---

## 6. データベースとベクトル検索

データベースとベクトル検索は移植可能なまま保ち、Windows ターゲットにしない。
ここでの「特化」は Windows API ではなく、規模に応じた方式選定を指す。

SQLite は据え置く（`docs/tech-selection.md` §4）。
`Microsoft.Data.Sqlite` で WAL モードを使い、読み取り並行性を確保し、書き込みはリポジトリ層で直列化する（`docs/architecture.md` §6）。
メモリマップド I/O（`mmap`）など SQLite 自体のチューニングは PoC-3 で計測する。

ベクトル検索の方式は規模と索引構築コストで選ぶ。

| 方式 | 特性 | 向く場面 |
|---|---|---|
| `sqlite-vec` | 総当りの厳密検索。索引構築が不要で導入が単純。規模が増えると遅い。 | 小〜中規模、簡便さ優先、近似不要 |
| HNSW（インメモリ） | 近似最近傍で高速。わずかに再現率を落とす。索引の構築と永続化を自前管理。 | 10 万枚規模で 1 秒以内目標 |
| DiskANN | ディスク常駐で超大規模に対応。索引構築が重い（数時間規模になりうる）。 | 数百万件以上、増分が少ない用途 |

本アプリの増分取り込み（フォルダ追加のたびに索引へ反映）では、DiskANN の重い索引構築は不向きである。
`docs/tech-selection.md` の「HNSW インメモリ索引 + SQLite 永続化」は規模と用途に整合する。
`sqlite-vec` は小規模時の簡便な代替として `IVectorIndex` の裏に差し替えられる。
最終判断は PoC-6 に委ねる。

---

## 7. ファイル監視、I/O、電源管理

### 7.1 ファイル監視

`System.IO.FileSystemWatcher` は内部で `ReadDirectoryChangesW` を使う移植可能な API で、まずこれを使う。
大量変更時の取りこぼしを避けるため `InternalBufferSize` を調整し、バッファ溢れ（`Error` イベント）時はフォルダ再走査でリカバリする。
さらに堅牢な大規模監視が要るなら、将来 USN ジャーナルや Windows Search 連携を `ILibraryWatcher` の裏で検討する。

### 7.2 電源とバッテリ対応

`docs/architecture.md` §5 はバッテリ駆動時の低負荷モードを要件に挙げる。
電源状態の取得には依存の軽い順に二つの経路がある。

- **Win32 `GetSystemPowerStatus`**：`kernel32` の関数で、AC/DC の別、バッテリ残量、バッテリセーバーの状態を返す。CsWin32 で呼べ、WinRT 依存や Windows ターゲットをほぼ要しない軽量経路である。
- **WinRT `PowerManager`**：`Windows.System.Power.PowerManager` は `EnergySaverStatus`（`Disabled`/`Off`/`On`）を提供する。`EnergySaverStatusChanged` や `PowerSourceKindChanged` などのイベントも備える。Windows App SDK には同等の `Microsoft.Windows.System.Power.PowerManager` がある。これらを購読して長時間ジョブを止め分けられる。

電源状態は `Core` か `Infrastructure` のインターフェース（たとえば `IPowerStatusProvider`）として公開し、Windows 実装を `Infrastructure.Windows` に置く。
`Jobs` はこのインターフェースを購読し、バッテリ駆動やバッテリセーバー時にワーカー数を絞り、ジョブ優先度（`docs/architecture.md` §5 の High/Normal/Low）を下げる。
これにより `Jobs` は `net8.0` のまま電源対応を実現できる。

### 7.3 GPU 使用率の抑制

GPU 使用率制限（同 §5）は、ORT のセッションオプション、EP の選択、ジョブの並行度で制御する。
推論側で無制限に並行化せず、並行度はジョブキューのワーカー数で一元管理する（`docs/di-composition.md` §5）。

---

## 8. オフラインとプライバシー制約の遵守

このアプリはネットワーク非接続で主要機能が動き（`docs/mvp-spec.md` AC-2）、解析データを外部送信しない（AC-7）。
Windows ML の EP 動的ダウンロードはこの制約と緊張するため、扱いを明示する。

`EnsureAndRegisterCertifiedAsync()` や `EnsureReadyAsync()` は、未インストールの EP を Windows がダウンロードする。
初回はネットワークと時間を要するため、完全オフライン運用やネットワーク制限環境では、EP を自前で同梱する「bring your own EPs」を採る。
CPU と DirectML（GPU）は Windows ML ランタイムに同梱されるため、これらだけならオフラインで動く。
NPU 加速をオフラインでも使うなら、対象ベンダー EP を同梱し、初回ダウンロードへ依存しない構成とする。

モデル本体はローカルの `models/` に置き（`docs/model-management.md`）、リポジトリには含めない。
推論と検索はローカルで完結し、画像/顔特徴量/埋め込みを外部送信しない方針（`docs/privacy.md`）は変えない。

---

## 9. パッケージングと CI への影響

### 9.1 CI

一部プロジェクトが `net8.0-windows10.0.x` になると、その部分は Linux でビルドできない。
CI をジョブ分割し、移植可能な範囲は Linux で、Windows 実装と `App` は Windows で検査する。

- **ubuntu ジョブ**：`Core` / `UI` / `Database` / `Search` / `Jobs` と各機能層（インターフェースとマネージド実装）、およびそれらのテストをビルド/テストする。
- **windows ジョブ**：`*.Windows` 実装と `App` を含むソリューション全体をビルドし、Windows 固有実装のテストと `App` の publish を検査する。

現状の CI は `ubuntu-latest` 単独（`.github/workflows/ci.yml`）なので、`windows-latest` ジョブの追加が要る。

### 9.2 配布

UI を Avalonia に保てば、self-contained の単一 EXE による配布の容易さを維持できる。
推論に Windows ML を使う場合は、ランタイム（およそ 41 MB）の扱いを選ぶ。

- フレームワーク依存：ランタイムを共有し、配布を小さく保つ。初回に Windows 側の準備を要する。
- 自己完結同梱：ランタイムを同梱し、オフライン初回起動を確実にする。配布サイズが増える。

オフライン要件（AC-2）を優先するなら、CPU/DirectML を確実に同梱し、必要に応じて NPU EP も同梱する構成を既定にする。

---

## 10. 既存インターフェースへの対応づけと無効化ゲート

Windows 実装は、すべて既存の契約インターフェースの裏に入る。

| 契約インターフェース | 層 | Windows 実装手段 | 置き場所 | フォールバック |
|---|---|---|---|---|
| `IThumbnailGenerator` | Imaging | StorageFile サムネイルキャッシュ＋WIC/MagicScaler | `Imaging.Windows` | SkiaSharp/MagicScaler（マネージド） |
| `IFileHasher` / `IPerceptualHasher` | Imaging | WIC でデコード後に計算 | `Imaging.Windows`（デコードのみ） | マネージドデコード |
| `IImageEmbeddingService` ほか推論 | AI | Windows ML（ExecutionProviderCatalog）＋ORT | `AI.Windows` | CPU EP のみの ORT |
| `IVectorIndex` | Search | 変更なし（移植可能） | `Search` | 線形探索/`sqlite-vec` |
| `ILibraryWatcher` | Infrastructure | FileSystemWatcher（必要なら USN/Search） | `Infrastructure`（既定）/`Infrastructure.Windows`（拡張） | 同左 |
| `IPowerStatusProvider`（新設） | Infrastructure | GetSystemPowerStatus / PowerManager | `Infrastructure.Windows` | 常時 AC 扱い |
| `IJobQueue` | Jobs | 変更なし（電源 IF を購読） | `Jobs` | 同左 |

加速の利用可否は、`docs/di-composition.md` §6 の能力問い合わせ（capability query）に反映する。
WIC コーデックの有無、EP の `ReadyState`、モデルファイルの有無を能力問い合わせ口で確認し、利用不可の機能は UI で無効表示にし、ジョブ投入前にスキップする。
無効化サービスに解析結果を捏造させない方針は変えない。

---

## 11. 段階的導入と PoC との対応

導入は Phase 0 の PoC に載せ、計測で確かめてから本実装へ進める。

- **PoC-1 / PoC-2（表示とサムネイル）**：StorageFile キャッシュ、WIC 縮小デコード、MagicScaler を比較し、AC-1 を満たす生成速度とスクロール FPS を計測する。
- **PoC-5（埋め込み）**：Windows ML の CPU/DirectML/NPU EP でスループットを比較し、既定 EP と量子化方針を決める。オフライン同梱の要否もここで判断する。
- **PoC-6（ベクトル検索）**：HNSW と `sqlite-vec` を 10 万件想定で比較する。
- **PoC-7 / PoC-8（顔と自然言語検索）**：モデル候補を ONNX で評価し、Windows ML 上の速度と精度を確かめる。

実装順は、まず `Core` の純粋性と既存インターフェースを保ったまま、`Imaging.Windows` と `AI.Windows` を薄く追加する形を採る。
インターフェースを変えないため、マネージド実装から Windows 実装への切り替えは DI 登録の差し替えで済む。

---

## 12. リスクと留意点

- **ターゲットフレームワークの連鎖**：機能層を不用意に Windows ターゲットへ変えると `UI`/`App` まで波及する。§3.2 の実装プロジェクト分離で連鎖を断つ。
- **CI の複雑化**：Windows ジョブの追加とジョブ分割が要る。移植可能部分の Linux テストは維持する。
- **Windows ML の初回ダウンロードとサイズ**：オフライン要件と緊張する。bring your own EPs と同梱方針で対処する。
- **未パッケージアプリの WinRT 制約**：呼べるのは Windows 標準型に限る。本アプリの用途では問題にならないが、サードパーティ WinRT 型には依存しない。
- **NPU EP のデバイス差**：ベンダーと OS バージョン（24H2 以降）に依存する。CPU/DirectML を常に動く土台とし、NPU は加点として扱う。
- **マネージドフォールバックの差異**：Windows 実装とマネージド実装の出力（サムネイル品質など）は完全には一致しないことがある。テストは許容差で比較する。

---

## 付録 A：PoC で評価するモデル候補の方向づけ

最終選定は PoC-5/7/8 で行う。
ここでは ONNX 化を前提に、評価する候補の方向だけを示す（具体の重みとリポジトリは PoC で確定し、本書では未確定とする）。

- **画像/テキスト埋め込み**：多言語対応の CLIP 系/SigLIP 系。自然言語検索の日本語クエリ精度（`docs/roadmap.md` Phase 3 のリスク）を満たす多言語モデルを優先して評価する。
- **自動タグ付け**：画像内容を多ラベルで推定するタガー系モデル。
- **顔検出と特徴量**：検出（SCRFD/RetinaFace 系）と特徴量（ArcFace 系）の組み合わせ。クラスタリング品質を PoC-7 で確かめる。

いずれも Windows ML 上で CPU/DirectML/NPU の速度を比較し、量子化（QDQ）後の精度低下が許容範囲かを確認する。

---

## 関連文書

- `docs/tech-selection.md`：技術選定。§3 の DirectML 前提は Windows ML へ更新が必要（別 Issue で反映）。
- `docs/architecture.md`：全体設計（§4 AI パイプライン、§5 ジョブ、§6 DB、§8 モデル管理）。
- `docs/di-composition.md`：合成ルートと生存期間（§2 参照境界、§6 無効化）。
- `docs/poc-tasks.md`：Phase 0 の PoC（PoC-1/2/3/5/6/7/8）。
- `docs/mvp-spec.md`：受け入れ条件（AC-1/2/7/8）。
- `docs/privacy.md` / `docs/model-management.md`：プライバシーとモデル管理。

## 参照（一次情報）

- Windows ML 概要と EP（Microsoft Learn）：<https://learn.microsoft.com/windows/ai/new-windows-ml/overview>、<https://learn.microsoft.com/windows/ai/new-windows-ml/supported-execution-providers>
- 実行プロバイダの取得/登録（Microsoft Learn）：<https://learn.microsoft.com/windows/ai/new-windows-ml/initialize-execution-providers>、<https://learn.microsoft.com/windows/ai/new-windows-ml/register-execution-providers>
- Windows ML の配布と要件（Microsoft Learn）：<https://learn.microsoft.com/windows/ai/new-windows-ml/distributing-your-app>
- DirectML の位置づけ（Microsoft Learn）：<https://learn.microsoft.com/windows/ai/directml/dml>
- WIC サムネイル/デコード（Microsoft Learn）：<https://learn.microsoft.com/uwp/api/windows.storage.storagefile.getthumbnailasync>、<https://learn.microsoft.com/uwp/api/windows.graphics.imaging.bitmapdecoder>
- 電源管理 API（Microsoft Learn）：<https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getsystempowerstatus>、<https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-power>
- WinRT/Win32 相互運用（Microsoft Learn）：<https://learn.microsoft.com/windows/apps/develop/platform/csharp-winrt/>、<https://learn.microsoft.com/windows/apps/desktop/modernize/winrt-com-interop-csharp>
- PhotoSauce MagicScaler：<https://github.com/saucecontrol/PhotoSauce>
- sqlite-vec：<https://github.com/asg017/sqlite-vec>
