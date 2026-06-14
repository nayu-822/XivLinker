# AGENTS.md

このリポジトリは、FF14向けのWPFデスクトップ補助アプリ **XivLinker** を .NET 10 で開発するためのプロジェクトです。

Codex / AIエージェントは、本ファイルを最初に読み、詳細な実装ルールは `docs/development/` 配下の各ドキュメントを参照してください。

## 開発の基本方針

- 対象フレームワークは原則として `.NET 10` とする。
- WPFアプリ本体は `net10.0-windows` をターゲットにする。
- Windows専用アプリとして設計する。
- UIはWPF + XAMLで実装する。
- 原則としてMVVM構成を採用する。
- Viewのコードビハインドには、UI固有の最小限の処理のみを書く。
- 業務ロジック、データ取得、ファイル解析、状態管理はViewModelまたはService層へ分離する。
- 不要な大規模リファクタリングを行わない。
- 既存の設計・命名・ディレクトリ構成を尊重する。
- 変更はできるだけ小さく、レビューしやすい単位にする。

## 想定する主なデータ取得元

本アプリでは、以下のデータ取得基盤を想定する。

- ACT / OverlayPlugin WebSocket
  - エリア移動イベント
  - 戦闘ログイベント
  - キャラクター情報
  - パーティ情報
- Lumina
  - FF14マスタデータ
  - アクション、アイテム、ジョブ、エリア、ステータス等
- キャラクター設定ファイル
  - ホットバー
  - キーバインド
  - HUD設定
  - マクロ
- FF14ログ / ACTログ
  - チャットログ
  - 戦闘ログ
  - システムログ
  - デバッグ用履歴

## FF14関連ツールとしての制約

- 自動操作を主目的とした機能は実装しない。
- ゲームクライアントのメモリ改変は行わない。
- パケット改変は行わない。
- パケット送信の偽装は行わない。
- 他プレイヤーに不利益を与える機能は実装しない。
- 取得データは、ローカル環境での補助表示・設定確認・ログ整理を主目的とする。
- 利用者にリスクがある機能を追加する場合は、必ずドキュメントに注意事項を記載する。
- 不明な仕様を推測で断定しない。
- 公式仕様でないDATファイル解析は、パッチ変更で壊れる前提で実装する。

## 推奨ディレクトリ構成

```text
src/
  XivLinker.App/
    Views/
    ViewModels/
    Resources/
    Themes/
    App.xaml
    MainWindow.xaml

  XivLinker.Domain/
    Models/
    ValueObjects/
    Events/
    Interfaces/

  XivLinker.Application/
    Services/
    UseCases/
    Abstractions/

  XivLinker.Infrastructure.Overlay/
    Clients/
    Models/
    Mappers/

  XivLinker.Infrastructure.Lumina/
    Services/
    Models/
    Mappers/

  XivLinker.Infrastructure.CharacterConfig/
    Readers/
    Parsers/
    Models/

  XivLinker.Infrastructure.Logs/
    Watchers/
    Parsers/
    Models/

  XivLinker.Tests/
    Unit/
    Integration/
```

プロジェクト名は **XivLinker** とする。

## ドキュメント配置ルール

ドキュメントは以下に配置する。

```text
docs/
  development/
    coding_rules.md
    ui_rules.md
    architecture.md
    setup.md
    testing.md

  features/
    overlay_plugin.md
    lumina.md
    character_config.md
    logs.md

  specs/
    data_sources.md
    events.md
    file_formats.md
```

### AGENTS.mdに書く内容

- Codex / AIエージェントが最初に読むべきルール
- 参照すべき詳細ドキュメント
- プロジェクト全体の方針
- 禁止事項
- 変更時の基本姿勢

### docs/development/ に書く内容

- コーディングルール
- UIルール
- テストルール
- セットアップ手順
- アーキテクチャ方針

### docs/features/ に書く内容

- 各機能ごとの仕様
- データ取得方式
- 外部ライブラリとの連携
- 既知の制約

### docs/specs/ に書く内容

- データ構造
- イベント定義
- ファイルフォーマット
- 解析済み仕様
- 未解析領域

## Codex作業ルール

Codexは以下の順序で作業すること。

1. `AGENTS.md` を読む。
2. 関連する `docs/development/` のルールを読む。
3. 関連する `docs/features/` または `docs/specs/` を読む。
4. 既存コードの構成を確認する。
5. 最小差分で実装する。
6. ビルド・テストを実行する。
7. 変更内容と確認結果をまとめる。

## 変更時の原則

- 既存の公開APIを不用意に変更しない。
- 既存のViewModel名、Service名、Model名を不用意に変更しない。
- 既存機能に影響する変更は、影響範囲を明記する。
- UI変更時は `docs/development/ui_rules.md` に従う。
- C#コード変更時は `docs/development/coding_rules.md` に従う。
- データ取得仕様を追加・変更した場合は `docs/features/` または `docs/specs/` を更新する。
- 解析中・未確定の仕様は `TODO` だけで放置せず、`Known limitations` または `Unknown fields` として明示する。

## 禁止事項

- 理由のない全面リファクタリング
- UIデザインの大幅変更を伴う無断修正
- XAMLへの大量ロジック記述
- ViewModelからViewを直接参照する実装
- async void の乱用
- ConfigureAwait の無秩序な追加
- 例外の握りつぶし
- マジックナンバーの放置
- 不明なバイナリ領域を推測で命名すること
- FF14クライアントへのメモリ改変
- パケット改変
- 自動操作やBOT化を目的とする実装

## ビルド・テストの基本コマンド

```powershell
dotnet restore
dotnet build
dotnet test
```

特定のソリューションファイルがある場合は、以下の形式を優先する。

```powershell
dotnet build ./XivLinker.sln
dotnet test ./XivLinker.sln
```

## UI作成時の注意

UIは古い業務アプリ風にならないようにする。

詳細は `docs/development/ui_rules.md` を参照すること。

特に以下を守る。

- 余白を十分に取る。
- 画面を罫線だらけにしない。
- デフォルトのWPFボタンをそのまま多用しない。
- 色・角丸・影・アイコン・タイポグラフィを統一する。
- ダークテーマを前提にしても、可読性を最優先する。
- FF14風に寄せすぎず、現代的なデスクトップツールとして整理する。
- 重要情報、警告、接続状態、ログは視線誘導を意識して配置する。

## コミット前確認

Codexは作業完了時に以下を確認する。

- ビルドが通るか
- テストが通るか
- 不要な差分がないか
- ドキュメント更新が必要ないか
- UI変更がルールに沿っているか
- FF14関連の禁止事項に触れていないか

## 参照ドキュメント

- `docs/development/coding_rules.md`
- `docs/development/ui_rules.md`
- `docs/development/architecture.md`
- `docs/features/overlay_plugin.md`
- `docs/features/lumina.md`
- `docs/features/character_config.md`
- `docs/features/logs.md`

## ドキュメント言語・文字コード

- リポジトリ内のドキュメントは原則として日本語で記述する。
- 対象は `README.md`、`docs/` 配下の文書、各プロジェクト配下の補足ドキュメントを含む。
- 文書ファイルの文字コードは UTF-8 を使用する。
- 文字化け防止のため、BOM 付き UTF-8 または BOM なし UTF-8 のいずれかで保存してよいが、同一ファイル内で不整合を起こさないこと。
- 外部仕様名、ライブラリ名、API 名など固有名詞は必要に応じて英語表記を併記してよい。
- アプリ内の表示文字列は原則として日本語を使用する。
- 設定キー、クラス名、API 名、外部サービス名など日本語化が不自然な識別子は英語のままでよい。
