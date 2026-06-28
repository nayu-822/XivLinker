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

- `KEYBIND.DAT` は DAT ヘッダーと command/binding の section ペアを読んで、key gesture を取り出します。復号後の末尾 `0x00` は reader で削らず、section ごとの null terminator を parser 側で検証します。
- `HOTBAR.DAT` は 16 byte ヘッダーの後ろを XOR 復号し、8 byte record の `CommandId / GroupId / HotbarId / SlotId / SlotTypeId` として読み取ります。
- HOTBAR と KEYBIND の照合は raw `HotbarId / SlotId` を基準に行います。`KEYBIND.DAT` 側では `HOTBAR_0_0` から `HOTBAR_9_11` に加えて、表示上の `HOTBAR_1_1` から `HOTBAR_10_12` も raw 座標へ正規化して扱います。
- Cross Hotbar は自動クラフト対象外として除外しています。
- `CraftSequenceExecutionPreparer` は sequence action -> hotbar slot -> key gesture の順で解決し、`CommandId` と現在ジョブに対応する `GroupId`、raw `HotbarId / SlotId` を使って未登録や未割当を fail-close で返します。
- 実行開始・失敗・完了の概要は `Information`、HOTBAR 復号や KEYBIND の詳細解決ログは `Debug` で出力します。
