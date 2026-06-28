# XivLinker.Infrastructure.CharacterConfig

FF14 のキャラクター設定ファイルを扱う Infrastructure レイヤーです。
現在の主な責務:

- キャラクター設定プロファイルの保持
- `HOTBAR.DAT` と `KEYBIND.DAT` のパス解決
- 選択中キャラクターの設定ファイル読み込み
- 自動クラフト実行前の HOTBAR / KEYBIND 再読込と実行準備
- 読み込み結果とエラー情報の整形
- `%LocalAppData%/XivLinker/character-profiles.json` との永続化

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

注意:

- DAT の詳細解析はまだ最小構成です。
- 自動クラフト実行前の準備では、保存済み `CharacterData` の生バイト列を使い回さず、実行直前に `HOTBAR.DAT` / `KEYBIND.DAT` を再読込します。
- HOTBAR / KEYBIND を解釈できない場合は fail-close で停止し、シーケンスは実行しません。
