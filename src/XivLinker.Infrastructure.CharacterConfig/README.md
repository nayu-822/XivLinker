# XivLinker.Infrastructure.CharacterConfig

FF14 のキャラクター設定ファイルを扱う Infrastructure レイヤーです。現在の主な役割は次のとおりです。

- キャラクター設定ディレクトリの登録と管理
- `HOTBAR.DAT` と `KEYBIND.DAT` の読込
- 自動クラフト実行前のホットバー・キーバインド解決
- 読込失敗や解析結果の診断ログ出力

主な構成:

- `Models/CharacterProfile.cs`
- `Models/CharacterData.cs`
- `Models/CharacterConfigFiles.cs`
- `Models/HotbarSlotEntry.cs`
- `Models/HotbarSlotKeyBinding.cs`
- `Models/KeybindEntry.cs`
- `Services/CharacterConfigPathResolver.cs`
- `Services/CharacterConfigDataService.cs`
- `Services/CharacterProfileStore.cs`
- `Services/CharacterConfigFileLoader.cs`
- `Services/DatFileContentReader.cs`
- `Services/HotbarDatReader.cs`
- `Services/KeybindDatReader.cs`
- `Services/CraftSequenceExecutionPreparer.cs`

現状の注意点:

- `KEYBIND.DAT` は DAT ヘッダーと section 構造を読んで、command と key gesture を取り出します。
- `HOTBAR.DAT` は DAT ヘッダー読込後、既知の候補レイアウトを順に試し、必要に応じて ActionId 周辺をアンカーにしてホットバー登録を推定します。
- Cross Hotbar は自動クラフト対象外として除外しています。
- `CraftSequenceExecutionPreparer` は sequence action -> hotbar slot -> key gesture の順で解決し、未登録や未割当を fail-close で返します。
- 解析に必要なログは `HotbarDatReader` / `KeybindDatReader` / `CraftSequenceExecutionPreparer` から `Information` レベルでも出力されるため、今後の実データ調整にも利用できます。
