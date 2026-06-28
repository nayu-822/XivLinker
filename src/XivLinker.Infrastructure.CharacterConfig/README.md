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

- `KEYBIND.DAT` は DAT ヘッダーと command/binding の section ペアを読んで、key gesture を取り出します。
- `HOTBAR.DAT` は 16 byte ヘッダーの後ろを XOR 復号し、8 byte record の `CommandId / GroupId / HotbarId / SlotId / SlotTypeId` として読み取ります。
- Cross Hotbar は自動クラフト対象外として除外しています。
- `CraftSequenceExecutionPreparer` は sequence action -> hotbar slot -> key gesture の順で解決し、`CommandId` と現在ジョブに対応する `GroupId`、`HotbarId / SlotId` を使って未登録や未割当を fail-close で返します。
- 解析に必要なログは `HotbarDatReader` / `KeybindDatReader` / `CraftSequenceExecutionPreparer` から `Information` レベルでも出力されるため、今後の実データ調整にも利用できます。
