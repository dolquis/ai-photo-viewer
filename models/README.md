# models/

AI モデルの配置ディレクトリ。詳細な方針は `docs/model-management.md` を参照。

## 方針

- モデル本体（ONNX ファイル等）は **リポジトリに含めない**。`.gitignore` で除外している。
- モデルはアプリ本体と分離してローカル管理し、ネットワーク非接続で推論する。
- カテゴリごと・バージョンごとにサブディレクトリへ配置する。

## ディレクトリ構成

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
  ocr/                      （Phase 5 以降）
  quality/                  （Phase 6 以降）
  upscale/                  （Phase 7 以降）
  denoise/                  （Phase 7 以降）
```

## metadata.json

各モデルディレクトリに、入力サイズ・正規化パラメータ・出力次元・ライセンス・
配布元 URL を記載した `metadata.json` を置く。アプリはこれを読んで前処理を構成する。

## MVP で必要なモデルカテゴリ

- 画像埋め込み（CLIP / SigLIP / MobileCLIP 系の軽量モデルを候補）
- テキスト埋め込み（画像埋め込みと対のテキスト塔）
- 画像タグ付け
- 顔検出
- 顔特徴量

## 取得手順

具体的なモデル選定は Phase 0 の技術検証（`docs/poc-tasks.md` PoC-5/-7/-8）で確定する。
確定後、このファイルにダウンロード元・バージョン・ライセンス・配置手順を追記する。

## ライセンス上の注意

各モデルのライセンスを確認し、再配布可否・商用利用可否を `metadata.json` に記録すること。
ライセンスが不明なモデルは同梱・案内しない。
