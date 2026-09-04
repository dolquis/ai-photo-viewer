# CLAUDE.md

このリポジトリの開発規約・Linear 連携・ビルド/テスト手順・レイヤ依存規約は
[`AGENTS.md`](AGENTS.md) に集約している。**作業前に必ず `AGENTS.md` を読むこと。**

要点（詳細は AGENTS.md）:

- Linear が管制塔、`docs/` が真実の源。Issue 単位で作業し、PR 本文に `DEV-xx` を必ずリンクする。
- 検証: `dotnet build AiPhotoViewer.sln` と `dotnet test` が通ること。提出前に `dotnet format`。
- `Core` は他層を参照しない。UI と推論はインターフェースで疎結合に保つ。
- Phase 0 の PoC は使い捨てとして `poc/` 配下に置く（`poc/README.md`）。
- モデル本体・DB・キャッシュ・ログは commit しない。
- `dolquis/agent-ops` からベンダリングした共有スキルの本文・`references/` は origin のコピー。
  この repo で直接編集しない（`AGENTS.md` §9）。
- repo docs は「定義」だけを持ち、状態語・進捗表を書かない（`AGENTS.md` §10）。docs を
  変更したら `python3 scripts/docs-lint.py --baseline .docs-lint-baseline.json` を実行する。

## アドバイザー（Fable）への相談

本節は Claude Code 専用（Advisor は Claude Code 固有機能のため）。判断を誤るとコストの大きい局面では、Advisor 機能でアドバイザー（Fable）に相談してから進める。助言は批判的に検討し、最終判断は自分で行う。前提としてアドバイザーを Fable に設定しておくこと（`settings.json` の `advisorModel: "fable"`、または `/advisor fable`）。未設定の環境では本節は無視してよい。

相談する場面の例:

- 複数ステップの作業で、実装方針・設計を確定する前の計画レビュー。
- レイヤ依存規約（`Core` は他層非参照）や UI / 推論の疎結合インターフェース設計を決めるとき。
- 大規模リファクタや、後戻りしにくい API 設計の確定前。
- 同じエラー・テスト（`dotnet test`）失敗が繰り返し、原因の切り分けに行き詰まったとき。
- 重要な変更を完了扱いにする前の独立チェック。

typo・コメントのみ・軽微で可逆な変更など計画の余地が小さい作業では使わない（トークンを消費し利用枠にも計上されるため）。Linear 管制塔運用や人間のレビューゲートの代替にはしない。
