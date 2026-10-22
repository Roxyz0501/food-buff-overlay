# Food Buff Overlay for ACT

プレイヤー自身の食事効果（ステータスID 48）を監視し、必要なときだけ通知するACTプラグインです。

作者: `Roxyz0501`

## 表示条件

- 食事効果がない: `食事切れ` を赤色で表示
- 残り時間が設定値以下: `食事効果 残り MM:SS` を橙色で表示
- 上記以外: オーバーレイを完全に非表示
- FFXIV未接続・キャラクター未取得時: 誤警告を避けるため非表示

現在の戦闘者スナップショットに加え、ACTの現在ログから食事効果の付与・解除を復元します。そのため、食事効果が付いた状態で本プラグインだけを途中ロードした場合にも対応します。FFXIVデータは5秒ごとに確認し、残り時間表示はローカルで1秒ごとに更新します。

警告開始時間は1～60分（初期値12分）で設定できます。設定画面の「位置調整プレビュー」を有効にすると、表示条件外でも配置調整用の表示を出せます。

## インストール

1. `FoodBuffOverlay.dll` をプラグイン用フォルダへコピーします。Release ZIP内のREADMEは任意です。
2. ACTの `Plugins` タブで `Browse...` を押し、DLLを選択します。
3. FFXIV Parsing Pluginより後ろの順番で本プラグインを有効にします。
4. `Food Buff Overlay` タブでプレビューをON、固定をOFFにして位置とサイズを調整します。
5. 調整後はプレビューをOFF、固定をONにします。

ゲームはフルスクリーンではなく、仮想フルスクリーン（ボーダーレス）で起動してください。

## ビルド

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

ビルドではプラグインとテストをコンパイルし、次のRelease用ファイルも生成します。終了後アップデータは起動しません。ACT終了後の手動置換が必要です。

- `dist\FoodBuffOverlay-vX.Y.Z.zip`
- `dist\FoodBuffOverlay-vX.Y.Z.sha256`

テストは次のように実行できます。

```powershell
.\tests\FoodBuffOverlay.Tests\bin\Release\net48\FoodBuffOverlay.Tests.exe
```

設定は `%APPDATA%\Advanced Combat Tracker\Config\FoodBuffOverlay.xml` に保存されます。
変更された設定だけを最大60秒ごとに保存し、プラグインの正常な終了時にも未保存分を保存します。オーバーレイ位置の変更は450ミリ秒の入力停止後にメモリへまとめ、同じ保存周期で書き込みます。設定ファイルは一時ファイルへ書いてから置換するため、保存失敗時は既存ファイルを保持します。

## 言語

設定画面から次の4言語へいつでも切り替えられ、再読み込みなしでUIとオーバーレイへ反映されます。

- English
- 日本語
- 简体中文
- 한국어

英語が既定・フォールバック言語です。旧Configなどで `Language` が未設定の初回だけWindowsのUI言語を確認し、`ja` 系は日本語、`zh` 系は簡体字中国語、`ko` 系は韓国語、それ以外は英語を選択してConfigへ保存します。保存後はWindows言語によって上書きされません。

Configには更新確認設定も追加されています。

```xml
<Language>ja</Language>
<CheckUpdatesOnStartup>true</CheckUpdatesOnStartup>
<SkippedVersion></SkippedVersion>
```

旧Configにこれらの要素がなくても読み込めます。

## GitHub Releases更新機能

起動時更新確認をONにすると、UIをブロックしない処理で最新の安定版Releaseを1回確認します。設定画面の「今すぐ確認」から手動確認も可能です。draftとprereleaseは除外し、SemVerで現在版と比較します。

更新は利用者が「更新」ボタンを押した場合だけ行います。ZIPとSHA-256マニフェストをHTTPSで取得し、GitHubの許可済みホスト、ファイルサイズ、SHA-256、ZIP展開先、アセンブリ名、ReleaseタグとDLLバージョンを検証します。ロード中のDLLは上書きしません。検証済みDLLの一時領域パスを画面に表示するため、ACTを終了してから手動で現行DLLを置き換え、ACTを再起動してください。プラグインから外部アップデータを起動することはありません。

### 現在のリポジトリ設定

更新元は、このプラグイン専用の公開リポジトリ [Roxyz0501/food-buff-overlay](https://github.com/Roxyz0501/food-buff-overlay) です。認証トークンを埋め込まず、GitHub Releases APIから安定版だけを確認します。

GitHubトークンはコードやReleaseへ埋め込まないでください。

### Release公開手順

1. `FoodBuffOverlay.csproj` のバージョンを `X.Y.Z` に更新します。
2. `build.ps1` をRelease構成で実行し、テストが通ることを確認します。
3. `vX.Y.Z` タグをpushします。
4. GitHub Actionsが公式ACT ZIPを一時取得して再ビルド・テストし、タグ、DLL、asset名を照合します。
5. 成功した場合だけ `FoodBuffOverlay-vX.Y.Z.zip` と `FoodBuffOverlay-vX.Y.Z.sha256` を安定版Releaseへ公開します。

GitHub Actionsの通常ジョブは`contents: read`、Releaseジョブだけを`contents: write`に限定しています。外部ActionはGitHub公式`actions/checkout`をコミットSHA固定で使用します。

SHA-256 assetがない、ZIPが破損している、想定外のDLL／バージョンが含まれる場合、更新は中止され通常のプラグイン機能を継続します。

## ライセンス

現時点ではライセンスは未設定です。利用・再配布条件を定めるLICENSEは、作者が方針を決定するまで追加していません。

## 開発支援

Roxyz0501の開発を支援したい場合は、設定画面の「支援」タブ、または次のリンクからKo-fiを利用できます。

- [Ko-fiでRoxyz0501を支援する](https://ko-fi.com/roxyz0501)

支援は完全に任意です。支援しなくてもすべての機能を利用でき、機能差はありません。起動時の案内、自動的なページ遷移、繰り返し通知、支援の有無による機能制限は行いません。

ACTを含む外部ツールの利用はFINAL FANTASY XIVの利用規約上の扱いを理解したうえで、自己責任で行ってください。本プラグインはゲームへの入力送信を行いません。
