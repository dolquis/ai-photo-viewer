# CLAUDE.md

このリポジトリの開発規約・Linear 連携・ビルド/テスト手順・レイヤ依存規約は
[`AGENTS.md`](AGENTS.md) に集約している。**作業前に必ず `AGENTS.md` を読むこと。**

要点（詳細は AGENTS.md）:

- Linear が管制塔、`docs/` が真実の源。Issue 単位で作業し、PR 本文に `DEV-xx` を必ずリンクする。
- 検証: `dotnet build AiPhotoViewer.sln` と `dotnet test` が通ること。提出前に `dotnet format`。
- `Core` は他層を参照しない。UI と推論はインターフェースで疎結合に保つ。
- Phase 0 の PoC は使い捨てとして `poc/` 配下に置く（`poc/README.md`）。
- モデル本体・DB・キャッシュ・ログは commit しない。
