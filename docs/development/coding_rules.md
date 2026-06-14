# docs/development/coding_rules.md

# コーディングルール

このドキュメントは、**XivLinker** の .NET 10 / WPF アプリ開発における一般的なコーディングルールを定義する。

## 基本方針

- C#は読みやすさを最優先する。
- 過度に技巧的なコードを書かない。
- 1つのクラスに責務を詰め込まない。
- UI、アプリケーションロジック、データ取得、ファイル解析を分離する。
- 外部入力、ファイル、WebSocket、ログ、バイナリ解析は失敗する前提で実装する。
- 例外を握りつぶさない。
- 不明な仕様はコメントで明示する。
- テスト可能な設計を優先する。

## Target Framework

WPFアプリ本体は以下を基本とする。

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Windows専用APIを使用する場合は、アプリ本体またはWindows専用Infrastructureプロジェクトへ閉じ込める。

## プロジェクト分割

推奨する分割は以下。

```text
App
  WPF起動、DI登録、View、ViewModel、Resource、Theme

Domain
  純粋なモデル、値オブジェクト、イベント、インターフェース

Application
  ユースケース、アプリケーションサービス、状態管理

Infrastructure
  外部データ取得、ファイルI/O、Lumina、OverlayPlugin、ログ監視

Tests
  Unit Test、Integration Test
```

Domain層はWPF、Lumina、OverlayPlugin、ファイルI/Oに依存しない。

## 命名規則

### クラス

```text
MainWindow
MainViewModel
OverlayWebSocketClient
LuminaActionRepository
HotbarDatReader
KeybindDatParser
ActLogWatcher
```

### インターフェース

```text
IOverlayClient
ILuminaRepository
ICharacterConfigReader
ILogWatcher
```

### 非同期メソッド

非同期メソッドは `Async` suffix を付ける。

```csharp
Task ConnectAsync(CancellationToken cancellationToken);
Task<IReadOnlyList<HotbarSlot>> ReadAsync(string characterDirectory, CancellationToken cancellationToken);
```

### イベント

イベントや通知用モデルは、内容が分かる名前にする。

```csharp
AreaChangedEvent
CharacterInfoUpdatedEvent
HotbarLoadedEvent
OverlayConnectionStateChangedEvent
```

## Nullable

- `Nullable` は有効化する。
- `string?` と `string` を明確に分ける。
- null許容を安易に広げない。
- nullチェックは境界部分で行う。
- ViewModelの表示用プロパティは、可能な限り空文字や空コレクションで扱う。

悪い例。

```csharp
public string? AreaName { get; set; }
```

良い例。

```csharp
public string AreaName { get; private set; } = string.Empty;
```

## async / await

- UIスレッドをブロックしない。
- `.Result` や `.Wait()` を使わない。
- 長時間処理、WebSocket、ファイル監視、Lumina読み込みは非同期化する。
- キャンセル可能な処理には `CancellationToken` を渡す。
- `async void` はイベントハンドラ以外で使わない。

良い例。

```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    await _overlayClient.ConnectAsync(cancellationToken);
}
```

## 例外処理

- 外部入力は必ず失敗を考慮する。
- 例外を握りつぶさない。
- ユーザーに見せるエラーとログに残すエラーを分ける。
- 解析不能なデータはアプリ全体を落とさず、警告として扱う。

良い例。

```csharp
try
{
    return await _reader.ReadAsync(path, cancellationToken);
}
catch (IOException ex)
{
    _logger.LogWarning(ex, "Failed to read character config file: {Path}", path);
    return CharacterConfigReadResult.Failed(path, ex.Message);
}
```

## ログ

- `Microsoft.Extensions.Logging` を使用する。
- `Console.WriteLine` は原則使わない。
- 個人情報やキャラクター名をログに出す場合は用途を明確にする。
- バイナリ解析時は、必要に応じて16進ダンプをDebugログに限定する。

ログレベルの目安。

```text
Trace       詳細な解析ログ
Debug       開発時の確認ログ
Information 起動、接続、読み込み成功など
Warning     復旧可能な異常
Error       機能失敗
Critical    アプリ継続困難
```

## MVVM

- Viewは表示に専念する。
- ViewModelは画面状態とコマンドを持つ。
- Serviceはデータ取得や処理を担当する。
- ViewModelからViewを直接参照しない。
- ViewModelにファイル解析ロジックを書かない。
- ViewModelにWebSocketの生処理を書かない。

推奨ライブラリ。

```text
CommunityToolkit.Mvvm
```

ViewModel例。

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentAreaName = string.Empty;

    [ObservableProperty]
    private bool isOverlayConnected;
}
```

## DI

- `Microsoft.Extensions.DependencyInjection` を使用する。
- App起動時にServiceを登録する。
- ViewModelもDIで生成できる構成を優先する。
- static singletonを乱用しない。

例。

```csharp
services.AddSingleton<IOverlayClient, OverlayWebSocketClient>();
services.AddSingleton<ILuminaRepository, LuminaRepository>();
services.AddTransient<MainViewModel>();
```

## 設定

- アプリ設定はJSONまたはOptionsパターンで管理する。
- パス、ポート、ログレベル、テーマ設定はコードに直書きしない。
- ユーザー環境依存の値は設定ファイルまたはUIから変更可能にする。

例。

```json
{
  "Overlay": {
    "WebSocketUrl": "ws://127.0.0.1:10501/ws",
    "ReconnectIntervalSeconds": 5
  },
  "Lumina": {
    "GameDataPath": ""
  },
  "CharacterConfig": {
    "CharacterDirectory": ""
  }
}
```

## ファイルI/O

- ファイル存在チェックを行う。
- 読み取り専用を基本とする。
- 書き込み機能を追加する場合はバックアップを作成する。
- DATファイルは壊すとユーザー環境に影響するため、初期段階では絶対に書き込まない。
- 文字コードを明示する。
- 大きなログファイルは全読み込みしない。

## バイナリ解析

- 既知フィールドと未知フィールドを分ける。
- 未知領域を推測で命名しない。
- サイズ、オフセット、エンディアンを明記する。
- 読み取りに失敗した場合は、どのオフセットで失敗したかログに残す。
- テストデータを用意する。
- パッチ差分に備えてバージョン情報を保持できる構造にする。

例。

```csharp
public sealed record UnknownBinarySegment(
    int Offset,
    int Length,
    byte[] Data);
```

## OverlayPlugin WebSocket

- 接続失敗してもアプリ全体を落とさない。
- 再接続処理を実装する。
- 受信JSONは型付きモデルに変換する。
- 未知イベントは破棄せずDebugログに残す。
- UIスレッドで受信ループを動かさない。
- WebSocket URLは設定可能にする。

## Lumina

- Luminaはマスタデータ参照として扱う。
- 現在状態の取得元として扱わない。
- IDから名称や詳細へ変換する用途に限定する。
- ゲームデータパスはユーザー設定可能にする。
- Lumina依存はInfrastructure層へ閉じ込める。

## テスト

- DomainとApplicationは単体テストを書く。
- ファイル解析はサンプルデータを使ったテストを書く。
- WebSocketやファイル監視はインターフェース化し、モック可能にする。
- UIテストは最初から重くしすぎない。
- 重要な変換処理、パーサー、状態遷移はテスト対象にする。

## コメント

コメントは「なぜそうしているか」を書く。

悪い例。

```csharp
// iを増やす
i++;
```

良い例。

```csharp
// HOTBAR.DATの末尾にはパッチ差分と思われる未知領域があるため、既知部分のみ解析する。
```

## コードフォーマット

- `.editorconfig` を使用する。
- usingは整理する。
- 不要な空行を増やさない。
- private fieldは `_camelCase` とする。
- public memberは `PascalCase` とする。
- varは型が明確な場合のみ使用する。

## 依存パッケージ

依存パッケージ追加時は以下を確認する。

- メンテナンスされているか
- ライセンスに問題がないか
- WPF / .NET 10 で利用可能か
- 過剰な依存ではないか
- 同等機能が標準ライブラリで実現できないか

## セキュリティ・安全性

- ユーザー環境のファイルを勝手に変更しない。
- 初期状態は読み取り専用にする。
- 外部通信先はローカルホストを基本とする。
- 個人情報、キャラクター名、ログ内容の扱いに注意する。
- クラッシュログに不要な個人情報を含めない。

## Pull Request / Codex完了報告

変更完了時は以下を報告する。

```text
概要:
- 何を変更したか

変更ファイル:
- path/to/file.cs
- path/to/file.xaml

確認:
- dotnet build 成功/未実施
- dotnet test 成功/未実施

注意点:
- 既知の制約
- 未対応事項
```
