# XivLinker.Infrastructure.Overlay

ACT / OverlayPlugin WebSocket 連携を扱う Infrastructure レイヤーです。

現在は主に以下を担当します。

- WebSocket 接続確認
- request / response 形式の `rseq` 管理
- broadcast イベント受信
- 現在プレイヤー状態の取得と保持

注意:

- 現在座標やジョブは `getCombatants` 応答と broadcast イベントを組み合わせて更新します。
- OverlayPlugin 側の JSON が想定外でも、例外を外へ漏らさず継続する方針です。
