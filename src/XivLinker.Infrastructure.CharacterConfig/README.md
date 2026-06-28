# XivLinker.Infrastructure.CharacterConfig

FF14 のキャラクター設定ファイルを扱う Infrastructure レイヤーです。現在の主な役割は次のとおりです。

- キャラクター設定ディレクトリの登録と管理
- `HOTBAR.DAT` と `KEYBIND.DAT` の読込
- 自動クラフト実行前の準備処理
- 読込失敗や未対応形式の診断ログ出力

主な構成:

- `Models/CharacterProfile.cs`
- `Models/CharacterData.cs`
- `Models/CharacterConfigFiles.cs`
- `Models/HotbarSlotEntry.cs`
- `Models/HotbarSlotKeyBinding.cs`
- `Services/CharacterConfigPathResolver.cs`
- `Services/CharacterConfigDataService.cs`
- `Services/CharacterProfileStore.cs`
- `Services/CharacterConfigFileLoader.cs`
- `Services/HotbarDatReader.cs`
- `Services/KeybindDatReader.cs`
- `Services/CraftSequenceExecutionPreparer.cs`

現状の注意点:

- `HOTBAR.DAT` / `KEYBIND.DAT` の実ファイル形式はまだ正式対応していません。
- `HotbarDatReader` / `KeybindDatReader` は現時点では解析を確定させず、先頭バイト列と XOR 結果をログに出して `UnsupportedCharacterConfigFormatException` を送出します。
- 自動クラフト実行前準備は fail-close を維持しており、DAT 形式が未対応の間はシーケンスを実行開始しません。
- 解析に必要なログは `CraftSequenceExecutionPreparer` から出力されるため、今後の実ファイル解析に利用できます。
