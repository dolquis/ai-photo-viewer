# モデル管理設計

引継ぎ文書 10章および 16章「解析結果には必ずモデル名・バージョンを保存する」に対応する。

## 1. 基本方針

- AI モデルはアプリ本体（実行ファイル/リポジトリ）と分離する。
- モデルはローカルの `models/` 配下に配置し、ネットワーク非接続で推論できる。
- モデルごとにバージョンを管理し、解析結果に使用モデル名・バージョンを記録する。
- 重量モデル（高精度）と軽量モデル（低スペック向け）を設定で切替可能にする。
- モデル変更時、影響を受ける解析を再キューして再解析できる。

## 2. モデルの配置とバージョニング

```text
models/
  image-embedding/
    <model-name>-<version>/
      model.onnx
      tokenizer.json        （テキスト埋め込み用）
      metadata.json
  tagging/
  face-detection/
  face-recognition/
  ...
```

- `metadata.json` に入力サイズ、正規化パラメータ、出力次元、ライセンス、配布元を記載。
- リポジトリにはモデル本体（重いバイナリ）を含めない（`.gitignore` で除外）。
  取得手順は `models/README.md` に記載する。

## 3. 解析結果へのモデル情報の記録

- 推論サービスは `ModelDescriptor`（名前・バージョン。`src/AI/Inference/ExecutionProvider.cs`）
  を公開する。
- `ImageEmbeddings` テーブルは `ModelName` / `ModelVersion` を必須カラムに持つ。
- タグ・顔特徴量・OCR・品質スコアも、生成に用いたモデル情報を保持する。
- これにより「どのモデルで解析済みか」を判定し、未解析・旧モデル解析を再解析対象に選別できる。

## 4. 推論抽象化

引継ぎ文書 10.3 のインターフェースを `src/AI/Inference/InferenceServices.cs` に定義済み。

| インターフェース | 役割 | MVP |
|---|---|---|
| `IImageEmbeddingService` | 画像/テキスト埋め込み | ○ |
| `ITaggingService` | 自動タグ付け | ○ |
| `IFaceDetectionService` | 顔検出 | ○ |
| `IFaceRecognitionService` | 顔特徴量抽出 | ○ |
| `IOcrService` | OCR | Phase 5 |
| `IQualityAssessmentService` | 画質診断 | Phase 6 |
| `IUpscaleService` | アップスケール | Phase 7 |
| `IDenoiseService` | ノイズ除去 | Phase 7 |

各サービスは ONNX Runtime 実装を持ち、`ExecutionProvider`（CPU/DirectML/NPU）と
モデルを差し替え可能にする。UI 層はこれらインターフェースのみに依存する。

## 5. MVP で必要なモデルカテゴリ

引継ぎ文書 10.2 に従い、MVP は以下に絞る。

- 画像埋め込みモデル（CLIP / SigLIP / MobileCLIP 系の軽量モデルを候補）
- テキスト埋め込みモデル（画像埋め込みと同一の対照学習モデルのテキスト塔）
- 顔検出モデル
- 顔特徴量モデル
- 画像タグ付けモデル

OCR・画質評価・アップスケール・ノイズ除去・セグメンテーションは後続フェーズ。

## 6. モデル未導入時の挙動

- 起動時に各モデルの存在を検査する。
- 未導入のモデルに依存する機能は無効化し、UI で導入を促す（クラッシュさせない）。
- 既定モデルの取得手順を設定画面・`models/README.md` から案内する。

## 7. 再解析

- モデル切替・バージョン更新時、対象段階のジョブを `AnalysisJobKind` 単位で再キューする。
- 既存結果は新モデルの結果で置き換える（モデル情報も更新）。
- ユーザーは画像/フォルダ単位で再解析を明示的に要求できる。
