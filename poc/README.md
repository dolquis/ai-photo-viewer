# PoC（Phase 0 技術検証）

`docs/poc-tasks.md` に対応する **使い捨ての技術検証コード** を置く場所。
本実装（`src/`）の品質基準とは分離し、計測と判断を目的とする。

実施順・依存・受け入れチェックの全体像は [`docs/p0-split-plan.md`](../docs/p0-split-plan.md)
（DEV-41 P0 分割計画）を参照。

## 規約

- **使い捨て前提**。PoC は本実装に直接マージしない。得られた知見・数値だけを `src/` と `docs/` に還元する。
- **`AiPhotoViewer.sln` には含めない**（CI のビルド/テスト対象外）。各 PoC は独立した最小プロジェクトとして単独実行する。
- 1 PoC = 1 ディレクトリ。命名は `poc/PoCN-<topic>/`（例: `poc/PoC1-ImageView/`）。
- 本実装の **契約インターフェース**（`src/**/＊Contracts.cs`、`src/AI/Inference/` 等）と整合する形で検証し、将来の移植先を意識する。
- 実行方法・前提（サンプルデータ、必要モデル）を各 PoC の `README.md` 先頭に書く。

## PoC ↔ Linear Issue ↔ docs

| PoC | Linear | ディレクトリ | 主な結果反映先 |
|---|---|---|---|
| PoC-1 画像表示性能 | DEV-42 | `poc/PoC1-ImageView/` | `docs/tech-selection.md`(1章) |
| PoC-2 サムネ/グリッド | DEV-43 | `poc/PoC2-ThumbnailGrid/` | `docs/tech-selection.md`(1章) |
| PoC-3 SQLite | DEV-44 | `poc/PoC3-Sqlite/` | `docs/architecture.md`(6章) |
| PoC-4 pHash 重複 | DEV-45 | `poc/PoC4-Duplicate/` | `docs/architecture.md`(4章) |
| PoC-5 ONNX/EP | DEV-50 | `poc/PoC5-OnnxRuntime/` | `docs/tech-selection.md`(3章) |
| PoC-6 ベクトル検索 | DEV-51 | `poc/PoC6-VectorSearch/` | `docs/tech-selection.md`(4章) |
| PoC-7 顔/人物 | DEV-53 | `poc/PoC7-Faces/` | `docs/roadmap.md`(Phase 4) |
| PoC-8 自然言語検索 | DEV-52 | `poc/PoC8-TextSearch/` | `docs/roadmap.md`(Phase 3) |

## 測定値テンプレート

各 PoC 完了時、`poc/PoCN-<topic>/RESULT.md` に下記を埋め、要点を該当 doc（上表）へ転記する。

```markdown
# PoC-N 測定結果

- 実施日 / 実施者:
- 計測環境（OS / CPU / GPU / RAM / .NET）:
- サンプルデータ（件数・解像度・出所）:
- 使用モデル（名前・バージョン）※AI 系のみ:

## 数値

| 指標 | 値 | 備考 |
|---|---|---|
|  |  |  |

## 合格判定

- 受け入れ条件を満たすか（Issue 記載の基準）: Yes / No
- 判明したリスク・制約:

## 結論 / 設計判断

- 採用 / 不採用 / 保留 と理由:
- `docs/` への反映先と反映済みか:
```
