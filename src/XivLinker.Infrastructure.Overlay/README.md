# XivLinker.Infrastructure.Overlay

ACT / OverlayPlugin WebSocket 連携を扱う Infrastructure レイヤーです。

現在は主に以下を担当します。
- WebSocket 接続管理
- `rseq` を使う request / response 処理
- `subscribe` / `startOverlayEvents` のような応答前提にしないコマンド送信
- OverlayPlugin からのイベント受信
- 送信直前 / 受信直後の raw JSON 通信ログ記録
- 現在プレイヤー状態の取得と更新

補足:
- 現在位置の表示は `getCombatants` の応答と `ChangeZone` などのイベントを組み合わせて更新します。
- OverlayPlugin 側の JSON 形式差異でアプリが落ちないよう、解析失敗は安全側に倒す方針です。
- `rseq` 付き response も通信ログから確認できるよう、`EventReceived` に流れない応答を含めてセッション層で記録します。
