# XivLinker.Infrastructure.Lumina

`XivLinker.Infrastructure.Lumina` は、Lumina を利用して FF14 のマスタデータを参照する実装プロジェクトです。

現在は主に以下を担当します。

- SqPack パスの解決と Lumina `GameData` 初期化
- クラフターアクション一覧の取得
- アクションアイコンの取得
- Territory / Map / ClassJob の名称解決
- ワールド座標から FF14 マップ座標への変換
- アイコン PNG のメモリキャッシュ
- `%LOCALAPPDATA%\XivLinker\Cache\Icons` へのディスクキャッシュ

アプリ UI は本プロジェクトへ直接依存せず、`ICrafterActionCatalogService` / `IGameDataService` 経由で利用します。
