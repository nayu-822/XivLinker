# XivLinker

XivLinker は、Final Fantasy XIV 向けの .NET 10 / WPF デスクトップ補助ツールです。

現時点ではアプリ基盤のみを実装しており、OverlayPlugin、Lumina、キャラクター設定ファイル、ログ連携などの機能はまだ未実装です。

## ビルド方法

```powershell
dotnet restore
dotnet build
dotnet test
```

## ファイルログ
- `%LOCALAPPDATA%\XivLinker\Logs\xivlinker-yyyyMMdd.log` に日別のファイルログを出力します。
- ファイルログの出力レベルは設定画面の「ログ設定」から `DEBUG / INFO / WARN / ERROR` を選択でき、設定は `%LOCALAPPDATA%\XivLinker\settings.json` に保存されます。
