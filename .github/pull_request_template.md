<!-- AGENTS.md の規約に従うこと。Linear が管制塔、docs/ が真実の源。 -->

## 概要

<!-- 何を・なぜ変更したか。1〜3 行で。 -->

## 関連 Issue（必須）

<!-- Linear のマジックワードで必ずひも付ける。
     完了で閉じる: Fixes DEV-xx / 関連のみ: Part of DEV-xx, Refs DEV-xx -->

- Refs DEV-

## 変更内容

<!-- 主な変更点を箇条書き -->

-

## チェックリスト

- [ ] `dotnet build AiPhotoViewer.sln` が警告/エラー 0 で通る
- [ ] `dotnet test` がパスする
- [ ] `dotnet format AiPhotoViewer.sln` 済み（差分なし）
- [ ] レイヤ依存規約を守っている（`Core` は他層を参照しない 等。AGENTS.md 4章）
- [ ] 設計変更があれば `docs/` を更新した
- [ ] AI モデル本体・ローカル DB・キャッシュ・ログを commit していない
- [ ] PoC は `poc/` 配下（使い捨て・`AiPhotoViewer.sln` 非同梱）／測定値を該当 doc に転記
- [ ] `type:human-gate` の場合、人間の確認を依頼している
