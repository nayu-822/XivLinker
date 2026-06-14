# docs/development/ui_rules.md

# UI作成ルール

このドキュメントは、**XivLinker** のWPF UIを作成・変更する際のルールを定義する。

目的は、古い業務アプリ風の見た目を避け、現代的で見やすく、保守しやすいUIを作ることである。

## UIの基本方針

- 情報を詰め込みすぎない。
- 画面全体に余白を持たせる。
- 罫線で区切るのではなく、余白・背景差・カードで区切る。
- 操作対象と表示情報を明確に分ける。
- 接続状態、現在エリア、ログ、警告を見分けやすくする。
- ダークテーマでもライトテーマでも読める設計にする。
- FF14風の雰囲気は参考にしてよいが、ゲームUIの完全再現は目指さない。
- ツールとしての視認性・操作性を優先する。

## 避けるべき見た目

以下は避ける。

- デフォルトWPFボタンをそのまま並べた画面
- グレー背景に小さいボタンが大量にある画面
- GroupBoxだらけの古い設定画面
- 罫線で全面を区切った表計算ソフト風画面
- 文字サイズが小さすぎる画面
- 余白がない画面
- 強すぎるグラデーション
- 意味のない影や装飾
- 原色の多用
- 重要度の違う情報が同じ強さで表示される画面

## レイアウト

基本の余白は以下を目安にする。

```text
画面外周: 24px
セクション間: 20px
カード内余白: 16px
項目間: 8px - 12px
ボタン内左右余白: 14px - 18px
```

画面構成の基本。

```text
Header
  アプリ名、接続状態、主要アクション

Main
  現在状態カード
  データソースカード
  詳細パネル
  ログ/イベント一覧

Footer / StatusBar
  最終更新時刻、警告、バージョン
```

## 推奨画面構成

初期画面は以下のような構成を推奨する。

```text
+--------------------------------------------------+
| XivLinker                          Connected ●   |
+--------------------------------------------------+
| Current State                                    |
|  Area: リムサ・ロミンサ：上甲板層                  |
|  Character: Example Name / Job                   |
+----------------------+---------------------------+
| Data Sources         | Details                   |
|  OverlayPlugin  ●    |  Selected data details    |
|  Lumina         ●    |                           |
|  Character DAT  ▲    |                           |
|  Logs           ●    |                           |
+----------------------+---------------------------+
| Event Log                                        |
|  12:34:56 Area changed ...                       |
+--------------------------------------------------+
```

## 色

色はResourceDictionaryで管理する。

直接XAMLに色を埋め込まない。

悪い例。

```xml
<Border Background="#202020" />
```

良い例。

```xml
<Border Background="{DynamicResource App.Brush.Surface}" />
```

推奨する色の役割。

```text
App.Brush.Background
App.Brush.Surface
App.Brush.SurfaceAlt
App.Brush.Border
App.Brush.TextPrimary
App.Brush.TextSecondary
App.Brush.TextMuted
App.Brush.Accent
App.Brush.Success
App.Brush.Warning
App.Brush.Error
```

色は役割で使い分ける。

- Accent: 主操作、選択状態
- Success: 接続成功、正常
- Warning: 一部失敗、注意
- Error: 接続不可、解析失敗
- TextMuted: 補足情報、時刻、ID

## ダークテーマ

FF14補助ツールとして、初期テーマはダークテーマを推奨する。

ただし、暗すぎる背景にしない。

推奨イメージ。

```text
背景: ほぼ黒ではなく、少し青みまたは紫みのあるダークグレー
カード: 背景より少し明るい色
境界線: 低コントラストの細い線
アクセント: 青、シアン、紫、金系のいずれか
文字: 白ではなく、少し抑えた明るいグレー
```

## ライトテーマ

将来的にライトテーマを追加できるよう、色は必ずリソース化する。

```text
Themes/
  DarkTheme.xaml
  LightTheme.xaml
  Shared.xaml
```

テーマ切替を想定し、固定色の直書きを避ける。

## タイポグラフィ

フォントサイズの目安。

```text
画面タイトル: 20 - 24
セクションタイトル: 16 - 18
本文: 13 - 14
補足: 12
ログ: 12 - 13
```

太字は使いすぎない。

- 画面タイトル
- セクションタイトル
- 重要な状態表示
- 警告

に限定する。

## ボタン

ボタンは目的に応じて種類を分ける。

```text
PrimaryButton
  主要操作
  例: 接続、読み込み、保存

SecondaryButton
  補助操作
  例: 再読み込み、開く、参照

DangerButton
  破壊的操作
  例: 削除、リセット

IconButton
  小さな補助操作
  例: 更新、コピー、設定
```

ボタンの配置ルール。

- 主要ボタンは右上または右下に配置する。
- 危険操作は通常操作から離す。
- ボタンを横に並べすぎない。
- アイコンのみのボタンにはToolTipを付ける。

## カード

情報のまとまりはカードで表現する。

```xml
<Border Style="{StaticResource CardStyle}">
    <StackPanel>
        <TextBlock Style="{StaticResource SectionTitleTextStyle}" Text="Current State" />
        ...
    </StackPanel>
</Border>
```

カードには以下を持たせる。

- 角丸
- 適度な余白
- 背景差
- 必要最小限の境界線
- 過剰でない影

## ステータス表示

接続状態は文字だけにしない。

推奨。

```text
● Connected
● Reconnecting
● Disconnected
▲ Partial
× Error
```

状態色を使う場合も、色だけに依存しない。  
必ずテキストまたはアイコンも併用する。

## ログ表示

ログは見やすさを優先する。

- 時刻を表示する。
- ログレベルを表示する。
- 長文は折り返しまたは詳細表示に逃がす。
- 自動スクロールはON/OFF可能にする。
- Error / Warning は視認しやすくする。
- 大量ログでUIが固まらないように仮想化を検討する。

ログ例。

```text
12:34:56 Info    OverlayPlugin connected.
12:35:01 Info    Area changed: 129 / Limsa Lominsa Upper Decks
12:35:04 Warning HOTBAR.DAT contains unknown segment.
12:35:10 Error   Failed to parse KEYBIND.DAT.
```

## DataGrid / List

DataGridを使う場合は、古い見た目になりやすいため注意する。

- 行の高さに余裕を持たせる。
- ヘッダーを強調しすぎない。
- 罫線を濃くしすぎない。
- 交互行色は控えめにする。
- 重要列を左側に置く。
- IDや内部値は必要時だけ表示する。

## 入力フォーム

設定画面では以下を守る。

- ラベルと入力欄を近づける。
- 説明文を必要に応じて添える。
- 入力エラーはその場で表示する。
- ファイルパスは手入力と参照ボタンの両方を用意する。
- 保存前に変更内容が分かるようにする。
- 設定保存後は明確にフィードバックする。

## アイコン

アイコンは統一する。

- 使用するアイコンセットを1つに決める。
- 線幅や塗りのスタイルを混在させない。
- アイコンだけで意味を伝えない。
- 重要操作にはテキストを併用する。

## アニメーション

アニメーションは控えめにする。

使ってよい例。

- 接続中インジケーター
- パネルの軽いフェード
- トースト通知
- ホバー時の軽い変化

避ける例。

- 常に動く背景
- 強い点滅
- 長い画面遷移
- 操作を妨げるアニメーション

## アクセシビリティ

- 文字サイズを小さくしすぎない。
- 色だけで状態を表現しない。
- コントラストを確保する。
- キーボード操作を妨げない。
- ToolTipを適切に設定する。
- 重要なボタンには分かりやすいテキストを付ける。
- 高コントラスト環境を無視しない。

## XAMLルール

- StyleはResourceDictionaryに分離する。
- 画面固有Styleと共通Styleを分ける。
- 色、余白、角丸、フォントサイズは可能な限りToken化する。
- 同じSetterを複数箇所に書かない。
- 複雑なXAMLはUserControlに分割する。
- Viewにロジックを書かない。

推奨構成。

```text
Resources/
  Brushes.xaml
  Spacing.xaml
  Typography.xaml
  Controls.xaml

Themes/
  DarkTheme.xaml
  LightTheme.xaml
```

## 共通リソース例

```xml
<sys:Double x:Key="App.Space.XS">4</sys:Double>
<sys:Double x:Key="App.Space.S">8</sys:Double>
<sys:Double x:Key="App.Space.M">12</sys:Double>
<sys:Double x:Key="App.Space.L">16</sys:Double>
<sys:Double x:Key="App.Space.XL">24</sys:Double>

<CornerRadius x:Key="App.CornerRadius.Card">12</CornerRadius>
<CornerRadius x:Key="App.CornerRadius.Control">8</CornerRadius>
```

## 画面追加時のチェックリスト

画面を追加・変更した場合、以下を確認する。

- 余白が十分か
- 情報を詰め込みすぎていないか
- 主要操作が分かりやすいか
- 接続状態やエラーが分かりやすいか
- 色を直書きしていないか
- 共通Styleを使っているか
- ViewModelに分離できる処理をViewに書いていないか
- 古いWPF標準UIのままになっていないか
- ダークテーマで読めるか
- 文字が小さすぎないか
- 色だけで状態を表現していないか
