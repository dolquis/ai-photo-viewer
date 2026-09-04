<!-- lint:allow-file heading-state,status,line-ref -->
# プロジェクト凍結ログ（AI Photo Viewer）

本書は、Linear 上の本プロジェクト Issue を一時凍結・解凍した記録である。
凍結は **削除ではなくアーカイブ**で行う（可逆・復元時に元ステータスを保持）。
Linear（管制塔）の状態と `docs/`（真実の源）を一致させるために残す。

---

## 2026-07-12 凍結（freeze）

### 背景

- 開発を一時凍結し、共有ワークスペース（Dev チーム, Linear 無料プラン）の枠を空ける。
- 無料プランの上限は**アクティブ（非アーカイブ）Issue 250件**。アーカイブ済みは上限にカウントされない。
- 凍結時点のワークスペース全体は **272 アクティブ**（上限超過）。本プロジェクト13件の退避で **259** に減少。

### 方針

- 本プロジェクト（`AI Photo Viewer Local AI Photo MVP` / `repo:ai-photo-viewer`）のアクティブ13件を**アーカイブ**する。
- **ステータスは変更しない**。解凍時に unarchive すれば、元の `Todo` / `Backlog` / `In Progress` に戻る。
- 傘 Issue DEV-40 は子を先にアーカイブし、DEV-40 を最後にした（カスケード挙動に依存しない順序）。
- 削除は採らない（30日後に完全消失し、PR の `Fixes DEV-xx` リンクバックも壊れるため）。

### 凍結した Issue（13件）

| Issue | 凍結時ステータス | UUID | 親 | タイトル |
|---|---|---|---|---|
| DEV-40  | In Progress | f0b2504e-3ac9-4d99-a946-1ba477aa3576 | -      | Photo Track（傘 tracking） |
| DEV-54  | In Progress | e23670ac-5045-4e3f-95ab-9fbc07a93836 | DEV-40 | Photo P0 result review |
| DEV-318 | In Progress | 2fcc6e49-f5ae-4d30-916c-07875b0f99a6 | DEV-40 | Human Gate: Stage 1 readiness 判定 |
| DEV-419 | Todo        | ff486246-35f9-415a-87c6-a714cf4e247b | -      | デコード可否（WIC コーデック）のゲート方針 |
| DEV-42  | Todo        | fa7c4d05-d6e4-4f19-9892-cff4e873aa78 | DEV-40 | Photo image view POC |
| DEV-43  | Todo        | 832e25d1-7af4-494b-b1c5-3e76ff9c5f99 | DEV-40 | Photo grid POC |
| DEV-44  | Todo        | 234c6f04-917f-46d5-8771-f09c0df8fed6 | DEV-40 | Photo DB POC |
| DEV-45  | Todo        | 7a787c38-c83a-4b14-a5c1-d1559fbbdd1d | DEV-40 | Photo duplicate POC |
| DEV-50  | Todo        | 03c48c72-f748-4731-b39c-c93b6258c477 | DEV-40 | Photo runtime POC |
| DEV-51  | Backlog     | af48ac42-5c5d-4f53-bfe4-4d757077545c | DEV-40 | Photo vector POC |
| DEV-52  | Backlog     | 08474373-6fe5-4750-a99c-b5c1175ee182 | DEV-40 | Photo text search POC |
| DEV-53  | Backlog     | d1c1526c-a93e-482c-ad8e-08ed666b0d2e | DEV-40 | Photo people POC |
| DEV-67  | Backlog     | 2f99a603-d7e1-4a34-8455-b8995582153c | -      | [Recurring] Linear control tower audit |

参考: 凍結前から既にアーカイブ済みの Done 6件（DEV-41 / DEV-276 / DEV-386 / DEV-387 / DEV-388 / DEV-417）。
凍結後、本プロジェクトのアクティブは **0件**、全19件がアーカイブ済み。

### バックアップ（完全スナップショット）

凍結時点の全19件（凍結対象13件＋既アーカイブ6件）の完全データ（説明本文・ラベル・親子・UUID 等）を
[`docs/freeze/ai-photo-viewer-linear-backup-2026-07-12.json`](freeze/ai-photo-viewer-linear-backup-2026-07-12.json) に保存した。
万一 Linear 側から復元できない事態でも、このスナップショットから内容を再作成できる。

### 未対応の手動タスク（要フォロー）

- **DEV-67 は Recurring（定期自動生成）Issue**。アーカイブしただけでは、次サイクルで新インスタンスが生成され枠を食い直す。
  完全に止めるには **Linear の Team 設定 → Recurring issues でテンプレートを停止/無効化**する（API 不可・設定画面での手動操作）。
- **枠はまだ 250 未満に達していない**（259）。新規 Issue 作成を回復するには、他プロジェクトの Done 済み Issue のアーカイブ等で追加に **9件以上**退避する必要がある。

---

## 解凍手順（thaw / 再開時）

1. 上表の各 UUID に対して `issueUnarchive` を実行する（ステータスは凍結時の値に復帰する）。

   ```graphql
   mutation { issueUnarchive(id: "<UUID>") { success } }
   ```

   一括する場合は本書の UUID 列を対象にする。DEV-40（傘）は最初に戻すと子との関係が見通しやすい。

2. DEV-67 を再稼働させる場合は、Team 設定の Recurring issues テンプレートを再度有効化する。

3. 解凍後、本プロジェクトのアクティブ数が 13 に戻ることを確認する。
