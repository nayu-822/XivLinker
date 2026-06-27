# XivLinker.Infrastructure.CharacterConfig

FF14 のキャラクター設定ファイルを扱うための Infrastructure レイヤーです。

現在の実装内容:

- キャラクター設定プロファイルの保持
- `HOTBAR.DAT` と `KEYBIND.DAT` のパス解決
- 選択中キャラクターの設定ファイル読み込み
- 読み込み結果とエラー情報の返却

主な構成:

- `Models/CharacterProfile.cs`
- `Models/CharacterData.cs`
- `Services/CharacterConfigPathResolver.cs`
- `Services/CharacterConfigDataService.cs`
- `Services/CharacterProfileStore.cs`

注意:

- DAT の詳細解析はまだ最小限です。
- 現時点では、ファイル存在確認と読み込み結果の取得を主目的にしています。
- 今後、自動クラフトやホットキー制御で利用しやすい形に解析結果を拡張する前提です。
