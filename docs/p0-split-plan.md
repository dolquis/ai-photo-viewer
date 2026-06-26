# P0 分割計画（Phase 0 技術検証）

DEV-41「Photo P0 split plan」の成果物。`docs/poc-tasks.md`（PoC 一覧・実施順）、
`poc/README.md`（PoC↔Issue↔docs 対応・測定テンプレート）、各 PoC Issue のレビュー追記に
分散していた P0 の作業分割・実施順・受け入れチェックを、真実の源（`docs/`）に集約する。

P0 の傘 Issue は DEV-40（Photo Track）。本書は傘配下の子 Issue を実行可能な単位へ分割し、
着手順・依存・完了チェックを一枚にまとめる。各 PoC の計測テンプレートは `poc/README.md`、
合格目安の原典は `docs/poc-tasks.md` を参照する。

---

## 1. スコープと前提

- Phase 0 の目的は、主要技術の実現性と性能を実測し、設計の前提（UI / ベクトル検索方式 /
  既定 EP / 採用モデル）を確定すること（`docs/roadmap.md` Phase 0）。
- PoC は**使い捨て前提**。本実装（`src/`）とは分離して `poc/PoCN-<topic>/` 配下で計測し、
  得られた数値・知見だけを `src/` と `docs/` に還元する（`poc/README.md`）。
- PoC は `AiPhotoViewer.sln` に含めない。各 PoC は独立した最小プロジェクトとして単独実行する。
- 検証は .NET 8 SDK を要する。各 PoC 実行後に `dotnet build AiPhotoViewer.sln`
  （DB 系は `dotnet test` も）で既存雛形を壊していないことを確認する。

## 2. PoC ↔ Issue ↔ 反映先

`poc/README.md` の対応表を基準とする。`area:*` ラベルは担当ディレクトリを示す（AGENTS.md §5）。

| PoC | Linear | area | ディレクトリ | 整合を取る本実装契約 | 結果反映先 |
|---|---|---|---|---|---|
| PoC-1 画像表示性能 | DEV-42 | photo-ui | `poc/PoC1-ImageView/` | `src/App/MainWindow.axaml` 雛形 | `docs/tech-selection.md` §1 |
| PoC-2 サムネ/グリッド | DEV-43 | image-core | `poc/PoC2-ThumbnailGrid/` | `src/Imaging/ImagingContracts.cs` | `docs/tech-selection.md` §1 |
| PoC-3 SQLite | DEV-44 | db | `poc/PoC3-Sqlite/` | `src/Database/Repositories.cs` | `docs/architecture.md` §6 |
| PoC-4 pHash 重複 | DEV-45 | image-core | `poc/PoC4-Duplicate/` | `src/Imaging/ImagingContracts.cs` | `docs/architecture.md` §4 |
| PoC-5 ONNX/EP | DEV-50 | ai-runtime | `poc/PoC5-OnnxRuntime/` | `src/AI/Inference/` | `docs/tech-selection.md` §3 |
| PoC-6 ベクトル検索 | DEV-51 | search | `poc/PoC6-VectorSearch/` | `src/Search/SearchContracts.cs` | `docs/tech-selection.md` §4 |
| PoC-7 顔/人物 | DEV-53 | faces | `poc/PoC7-Faces/` | `src/AI/Inference/`, `src/Core/Domain/Faces.cs` | `docs/roadmap.md` Phase 4 |
| PoC-8 自然言語検索 | DEV-52 | search | `poc/PoC8-TextSearch/` | `src/Search/SearchContracts.cs` | `docs/roadmap.md` Phase 3 |

集約レビューは DEV-54（Photo P0 result review）。Stage 1 着手可否の人間判断は
DEV-318（Human Gate: Stage 1 readiness 判定、`gate:human-required`）で別建てとする
（設計と人間ゲートの分離。`docs/linear-conventions.md` §7.1）。

## 3. 実施順と依存関係

`docs/poc-tasks.md`「実施順と依存関係」を Issue 粒度へ展開する。同一波内は並行可。

```
Wave 1（閲覧基盤・Phase 1 直結） : DEV-42  DEV-43  DEV-44      ← 相互依存なし、並行
Wave 2（重複・推論基盤・Phase 2）: DEV-45  DEV-50             ← Wave 1 と独立に着手可
Wave 3（埋め込み利用）          : DEV-51 ← DEV-50
                                 DEV-52 ← DEV-50, DEV-51
Wave 4（人物・Phase 4）         : DEV-53                       ← DEV-50 の前処理知見を流用
```

- DEV-51（ベクトル検索）は DEV-50（ONNX 埋め込み）が生成する埋め込みを入力に使うため、
  DEV-50 の完了後に着手する。
- DEV-52（自然言語検索）はテキスト埋め込みと画像埋め込みの整合を見るため、
  DEV-50 と DEV-51 の双方に依存する。
- Linear 上のブロック関係: DEV-41 が DEV-42 / DEV-43 をブロック（本計画確定が前提）。

## 4. 各 PoC の受け入れチェック

合格目安は `docs/poc-tasks.md` の表が原典。ここでは「着手前の Ready」「完了の Done」を
チェックリスト化する。各 PoC 共通で、完了時に `poc/PoCN-<topic>/RESULT.md`
（`poc/README.md` のテンプレート）を埋め、要点を反映先 doc へ転記する。

| PoC / Issue | 合格目安（原典） | 完了チェック |
|---|---|---|
| PoC-1 / DEV-42 | 数十 MP 画像の表示・操作が体感遅延なし | 表示・拡大縮小・回転の応答とメモリを RESULT.md に記録。AC-1/AC-4 の前提を確認 |
| PoC-2 / DEV-43 | 5 万枚グリッドが滑らかにスクロール | 仮想化スクロール FPS とサムネ生成/キャッシュ I/O を記録。AC-1 の根拠を確定 |
| PoC-3 / DEV-44 | 一覧ページング取得が 100ms 未満 | 10 万行で登録/更新/ページング、INDEX 効果、WAL 並行性を記録。`dotnet test` 通過 |
| PoC-4 / DEV-45 | 加工違いペアをハミング距離で安定検出 | pHash/dHash/aHash の速度・検出精度を比較記録。採用ハッシュを §4 へ反映 |
| PoC-5 / DEV-50 | CPU のみでも実用的なスループット | CPU/DirectML EP の速度比較と前処理コストを記録。既定 EP と CPU フォールバック実用性を判定（AC-8） |
| PoC-6 / DEV-51 | 10 万件で近傍検索 1 秒以内 | 線形/HNSW/`sqlite-vec` の速度・精度・メモリを比較。ベクトル検索方式を確定 |
| PoC-7 / DEV-53 | 同一人物が安定して同一クラスタ | 顔検出の精度・速度、特徴量クラスタリング品質を記録。特徴量がローカル保存に限ること（AC-7）を確認 |
| PoC-8 / DEV-52 | 「夜の神社」等で妥当な上位結果 | 日本語クエリでの検索妥当性とテキスト/画像埋め込みの整合を記録 |

## 5. P0 完了条件（DEV-54 で判定）

- 全 PoC の RESULT.md が揃い、要点が反映先 doc（§2）へ転記済み。
- 設計判断が確定: UI フレームワーク（Avalonia の仮想化性能）、ベクトル検索方式
  （HNSW vs `sqlite-vec`）、既定 EP（CPU フォールバックの実用性）、採用モデル
  （埋め込み・タグ・顔検出・顔特徴量）（`docs/poc-tasks.md`「検証で確定させる設計判断」）。
- 数値が `docs/tech-selection.md` / `docs/roadmap.md` Phase 0 完了条件に反映済み。
- 上記を DEV-54 で集約レビューし、Stage 1 着手可否を DEV-318（人間ゲート）へ引き継ぐ。

## 6. 運用上の注意

- 実装 PoC Issue（`agent:codex-impl`）は Codex 実行ルーティングであり、Codex への
  assign / delegate / mention は人間 lead の明示許可があるときのみ（AGENTS.md §2、
  `docs/linear-conventions.md` §2.1）。Claude は分割・整理・関連付けと指示文の下書きまで行う。
- モデル本体・DB・キャッシュ・ログは commit しない（AGENTS.md §9）。
- 本計画は分割と順序の確定が目的であり、PoC の実測値そのものは各 Issue / RESULT.md で扱う。
