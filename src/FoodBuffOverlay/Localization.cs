using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FoodBuffOverlay
{
    internal static class Localization
    {
        public const string English = "en";
        public const string Japanese = "ja";
        public const string Chinese = "zh-CN";
        public const string Korean = "ko";

        private static readonly Dictionary<string, Dictionary<string, string>> Resources =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [English] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PluginTab"]="Food Buff Overlay", ["HeaderSubtitle"]="Shows food warnings only when needed",
                    ["TabSettings"]="Settings", ["TabUpdate"]="Updates", ["TabSupport"]="Support", ["Language"]="Language",
                    ["LanguageHint"]="Changes are applied immediately.", ["Enabled"]="Enable overlay",
                    ["Locked"]="Lock position and size (click-through)", ["Preview"]="Show positioning preview",
                    ["WarningThreshold"]="Warn when remaining time is", ["MinutesSuffix"]="minutes or less (default: 12)",
                    ["Opacity"]="Overlay opacity", ["Hint"]="The overlay is hidden unless a warning condition is met.\r\nTo position it, unlock it, enable preview, and drag the overlay.",
                    ["Connected"]="● Reading FFXIV data", ["Connecting"]="● Waiting for connection",
                    ["AuthorVersion"]="Author: Roxyz0501   •   v{0}",
                    ["UpdateSection"]="Updates", ["CheckStartup"]="Check for updates when the plugin starts",
                    ["CheckNow"]="Check now", ["Checking"]="Checking for updates…",
                    ["RepositoryNotConfigured"]="Update repository is not configured.", ["UpToDate"]="You are using the latest version ({0}).",
                    ["NoStableRelease"]="No stable release was found.", ["UpdateAvailable"]="Update available: {0} → {1}\r\n{2}",
                    ["UpdateNow"]="Update", ["Later"]="Later", ["UpdateSkipped"]="Version {0} was skipped.",
                    ["PreparingUpdate"]="Downloading and verifying the update…", ["UpdatePrepared"]="Update verified. After closing ACT, manually copy this file over FoodBuffOverlay.dll, then restart ACT:\r\n{0}", ["UpdatePreparedStatus"]="Update verified. Close ACT and manually replace FoodBuffOverlay.dll, then restart ACT.",
                    ["UpdateFailed"]="Update failed: {0}", ["CheckFailed"]="Update check failed: {0}",
                    ["SupportHeading"]="Support Roxyz0501's development", ["SupportBody"]="If this plugin is useful to you, you can support its development through Ko-fi.",
                    ["SupportOptional"]="Support is completely optional. Every feature remains available with no differences if you do not support.",
                    ["SupportButton"]="Support Roxyz0501 on Ko-fi", ["BrowserOpened"]="Ko-fi opened in your default browser.", ["LinkFailed"]="Could not open the link: {0}",
                    ["OverlayPreview"]="POSITION PREVIEW", ["OverlayPreviewDetail"]="Drag to move • resize from an edge",
                    ["OverlayMissing"]="FOOD EXPIRED", ["OverlayMissingDetail"]="No food effect is active",
                    ["OverlayRemaining"]="FOOD  {0} REMAINING", ["OverlayExpiringDetail"]="Your food effect will expire soon",
                    ["Started"]="Food Buff Overlay: Started", ["Stopped"]="Food Buff Overlay: Stopped", ["SaveError"]="Food Buff Overlay: Could not save settings — {0}",
                    ["FfxivPluginMissing"]="FFXIV ACT Plugin is not enabled.", ["RepositoryNotReady"]="Waiting for the FFXIV data repository.", ["DataApiMissing"]="The FFXIV combatant data API is unavailable.",
                    ["UpdateAssetMissing"]="The required release asset or SHA-256 manifest is missing.", ["RestartAct"]="Exit and restart ACT to complete the update.",
                    ["NetworkFailure"]="Could not contact GitHub. The plugin will continue normally.", ["InvalidPackage"]="The downloaded update package is invalid.",
                    ["HashFailed"]="SHA-256 verification failed.",
                    ["UpdateFailureGeneric"]="The update could not be prepared safely.", ["DataReadFailed"]="Could not read FFXIV data.", ["BrowserFailure"]="The default browser could not be started."
                    ,["FallbackProbe"]="English fallback"
                },
                [Japanese] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PluginTab"]="食事バフオーバーレイ", ["HeaderSubtitle"]="食事切れと残り時間を、必要なときだけ通知します",
                    ["TabSettings"]="設定", ["TabUpdate"]="更新", ["TabSupport"]="支援", ["Language"]="言語",
                    ["LanguageHint"]="変更はすぐに反映されます。", ["Enabled"]="オーバーレイを有効にする",
                    ["Locked"]="位置とサイズを固定（クリック透過）", ["Preview"]="位置調整プレビューを表示する",
                    ["WarningThreshold"]="警告を表示する残り時間", ["MinutesSuffix"]="分以下（初期値 12分）",
                    ["Opacity"]="オーバーレイの透明度", ["Hint"]="通常は表示条件に当てはまらない限り完全に非表示です。\r\n位置調整時は固定をOFFにしてプレビューをONにし、オーバーレイをドラッグしてください。",
                    ["Connected"]="● FFXIVデータ取得中", ["Connecting"]="● 接続待ち",
                    ["AuthorVersion"]="作者: Roxyz0501   •   v{0}",
                    ["UpdateSection"]="更新", ["CheckStartup"]="プラグイン起動時に更新を確認する",
                    ["CheckNow"]="今すぐ確認", ["Checking"]="更新を確認しています…",
                    ["RepositoryNotConfigured"]="更新用GitHubリポジトリが未設定です。", ["UpToDate"]="最新版を使用しています（{0}）。",
                    ["NoStableRelease"]="安定版Releaseが見つかりません。", ["UpdateAvailable"]="更新があります: {0} → {1}\r\n{2}",
                    ["UpdateNow"]="更新する", ["Later"]="後で", ["UpdateSkipped"]="バージョン {0} をスキップしました。",
                    ["PreparingUpdate"]="更新をダウンロードして検証しています…", ["UpdatePrepared"]="更新を検証しました。ACTを終了後、次のファイルをFoodBuffOverlay.dllへ手動で上書きし、ACTを再起動してください:\r\n{0}", ["UpdatePreparedStatus"]="更新を検証しました。ACTを終了し、FoodBuffOverlay.dllを手動で置き換えてから再起動してください。",
                    ["UpdateFailed"]="更新に失敗しました: {0}", ["CheckFailed"]="更新確認に失敗しました: {0}",
                    ["SupportHeading"]="Roxyz0501の開発を支援", ["SupportBody"]="このプラグインが役立ったと感じた場合は、Ko-fiから開発を支援できます。",
                    ["SupportOptional"]="支援は完全に任意です。支援しなくてもすべての機能を利用でき、機能差はありません。",
                    ["SupportButton"]="Ko-fiでRoxyz0501を支援する", ["BrowserOpened"]="既定のブラウザでKo-fiを開きました。", ["LinkFailed"]="リンクを開けませんでした: {0}",
                    ["OverlayPreview"]="表示位置プレビュー", ["OverlayPreviewDetail"]="ドラッグで移動・端でサイズ変更",
                    ["OverlayMissing"]="食事切れ", ["OverlayMissingDetail"]="食事効果がついていません",
                    ["OverlayRemaining"]="食事効果  残り {0}", ["OverlayExpiringDetail"]="まもなく食事効果が切れます",
                    ["Started"]="Food Buff Overlay: 起動済み", ["Stopped"]="Food Buff Overlay: 停止", ["SaveError"]="Food Buff Overlay: 設定保存エラー — {0}",
                    ["FfxivPluginMissing"]="FFXIV ACT Pluginが有効ではありません。", ["RepositoryNotReady"]="FFXIVデータの準備待ちです。", ["DataApiMissing"]="FFXIVの戦闘者データAPIが見つかりません。",
                    ["UpdateAssetMissing"]="必要なRelease assetまたはSHA-256マニフェストがありません。", ["RestartAct"]="更新を完了するにはACTを終了して再起動してください。",
                    ["NetworkFailure"]="GitHubへ接続できませんでした。プラグインの通常機能は継続します。", ["InvalidPackage"]="ダウンロードした更新パッケージが不正です。",
                    ["HashFailed"]="SHA-256検証に失敗しました。",
                    ["UpdateFailureGeneric"]="更新を安全に準備できませんでした。", ["DataReadFailed"]="FFXIVデータを読み取れませんでした。", ["BrowserFailure"]="既定ブラウザを起動できませんでした。"
                },
                [Chinese] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PluginTab"]="食物增益悬浮窗", ["HeaderSubtitle"]="仅在需要时显示食物效果提醒",
                    ["TabSettings"]="设置", ["TabUpdate"]="更新", ["TabSupport"]="支持", ["Language"]="语言", ["LanguageHint"]="更改会立即生效。",
                    ["Enabled"]="启用悬浮窗", ["Locked"]="锁定位置和大小（鼠标穿透）", ["Preview"]="显示位置调整预览",
                    ["WarningThreshold"]="剩余时间达到以下值时提醒", ["MinutesSuffix"]="分钟以下（默认 12 分钟）", ["Opacity"]="悬浮窗透明度",
                    ["Hint"]="不符合提醒条件时悬浮窗会完全隐藏。\r\n调整位置时请解除锁定、开启预览，然后拖动悬浮窗。",
                    ["Connected"]="● 正在读取FFXIV数据", ["Connecting"]="● 等待连接", ["AuthorVersion"]="作者: Roxyz0501   •   v{0}",
                    ["UpdateSection"]="更新", ["CheckStartup"]="插件启动时检查更新", ["CheckNow"]="立即检查", ["Checking"]="正在检查更新…",
                    ["RepositoryNotConfigured"]="尚未配置更新用GitHub仓库。", ["UpToDate"]="当前已是最新版本（{0}）。", ["NoStableRelease"]="未找到稳定版Release。",
                    ["UpdateAvailable"]="发现更新: {0} → {1}\r\n{2}", ["UpdateNow"]="更新", ["Later"]="稍后", ["UpdateSkipped"]="已跳过版本 {0}。",
                    ["PreparingUpdate"]="正在下载并验证更新…", ["UpdatePrepared"]="更新已验证。退出ACT后，请手动将此文件覆盖到FoodBuffOverlay.dll，然后重新启动ACT：\r\n{0}", ["UpdatePreparedStatus"]="更新已验证。请退出ACT，手动替换FoodBuffOverlay.dll，然后重新启动ACT。",
                    ["UpdateFailed"]="更新失败: {0}", ["CheckFailed"]="检查更新失败: {0}",
                    ["SupportHeading"]="支持Roxyz0501的开发", ["SupportBody"]="如果这个插件对您有帮助，可以通过Ko-fi支持开发。",
                    ["SupportOptional"]="支持完全自愿。即使不支持，也可以使用全部功能，不存在任何功能差异。",
                    ["SupportButton"]="在Ko-fi上支持Roxyz0501", ["BrowserOpened"]="已在默认浏览器中打开Ko-fi。", ["LinkFailed"]="无法打开链接: {0}",
                    ["OverlayPreview"]="位置预览", ["OverlayPreviewDetail"]="拖动移动 • 从边缘调整大小", ["OverlayMissing"]="食物效果已结束", ["OverlayMissingDetail"]="当前没有食物效果",
                    ["OverlayRemaining"]="食物效果剩余 {0}", ["OverlayExpiringDetail"]="食物效果即将结束",
                    ["Started"]="Food Buff Overlay: 已启动", ["Stopped"]="Food Buff Overlay: 已停止", ["SaveError"]="Food Buff Overlay: 保存设置失败 — {0}",
                    ["FfxivPluginMissing"]="未启用FFXIV ACT Plugin。", ["RepositoryNotReady"]="正在等待FFXIV数据。", ["DataApiMissing"]="无法使用FFXIV战斗对象数据API。",
                    ["UpdateAssetMissing"]="缺少所需的Release asset或SHA-256清单。", ["RestartAct"]="请退出并重新启动ACT以完成更新。",
                    ["NetworkFailure"]="无法连接GitHub。插件的正常功能将继续运行。", ["InvalidPackage"]="下载的更新包无效。", ["HashFailed"]="SHA-256验证失败。",
                    ["UpdateFailureGeneric"]="无法安全地准备更新。", ["DataReadFailed"]="无法读取FFXIV数据。", ["BrowserFailure"]="无法启动默认浏览器。"
                },
                [Korean] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PluginTab"]="음식 버프 오버레이", ["HeaderSubtitle"]="필요할 때만 음식 버프 경고를 표시합니다",
                    ["TabSettings"]="설정", ["TabUpdate"]="업데이트", ["TabSupport"]="후원", ["Language"]="언어", ["LanguageHint"]="변경 사항은 즉시 적용됩니다.",
                    ["Enabled"]="오버레이 사용", ["Locked"]="위치와 크기 잠금(클릭 통과)", ["Preview"]="위치 조정 미리보기 표시",
                    ["WarningThreshold"]="남은 시간이 다음 이하일 때 경고", ["MinutesSuffix"]="분 이하(기본값 12분)", ["Opacity"]="오버레이 투명도",
                    ["Hint"]="경고 조건에 해당하지 않으면 오버레이가 완전히 숨겨집니다.\r\n위치 조정 시 잠금을 해제하고 미리보기를 켠 뒤 드래그하세요.",
                    ["Connected"]="● FFXIV 데이터 읽는 중", ["Connecting"]="● 연결 대기 중", ["AuthorVersion"]="제작자: Roxyz0501   •   v{0}",
                    ["UpdateSection"]="업데이트", ["CheckStartup"]="플러그인 시작 시 업데이트 확인", ["CheckNow"]="지금 확인", ["Checking"]="업데이트 확인 중…",
                    ["RepositoryNotConfigured"]="업데이트용 GitHub 저장소가 설정되지 않았습니다.", ["UpToDate"]="최신 버전을 사용 중입니다({0}).", ["NoStableRelease"]="안정 버전 Release를 찾지 못했습니다.",
                    ["UpdateAvailable"]="업데이트 있음: {0} → {1}\r\n{2}", ["UpdateNow"]="업데이트", ["Later"]="나중에", ["UpdateSkipped"]="버전 {0}을 건너뛰었습니다.",
                    ["PreparingUpdate"]="업데이트 다운로드 및 검증 중…", ["UpdatePrepared"]="업데이트를 검증했습니다. ACT를 종료한 뒤 이 파일을 FoodBuffOverlay.dll에 수동으로 덮어쓰고 ACT를 다시 시작하세요:\r\n{0}", ["UpdatePreparedStatus"]="업데이트를 검증했습니다. ACT를 종료하고 FoodBuffOverlay.dll을 수동으로 교체한 뒤 다시 시작하세요.",
                    ["UpdateFailed"]="업데이트 실패: {0}", ["CheckFailed"]="업데이트 확인 실패: {0}",
                    ["SupportHeading"]="Roxyz0501의 개발 후원", ["SupportBody"]="이 플러그인이 유용했다면 Ko-fi를 통해 개발을 후원할 수 있습니다.",
                    ["SupportOptional"]="후원은 완전히 선택 사항입니다. 후원하지 않아도 모든 기능을 차이 없이 사용할 수 있습니다.",
                    ["SupportButton"]="Ko-fi에서 Roxyz0501 후원하기", ["BrowserOpened"]="기본 브라우저에서 Ko-fi를 열었습니다.", ["LinkFailed"]="링크를 열 수 없습니다: {0}",
                    ["OverlayPreview"]="위치 미리보기", ["OverlayPreviewDetail"]="드래그하여 이동 • 가장자리에서 크기 조절", ["OverlayMissing"]="음식 버프 종료", ["OverlayMissingDetail"]="음식 버프가 없습니다",
                    ["OverlayRemaining"]="음식 버프 남은 시간 {0}", ["OverlayExpiringDetail"]="음식 버프가 곧 종료됩니다",
                    ["Started"]="Food Buff Overlay: 시작됨", ["Stopped"]="Food Buff Overlay: 중지됨", ["SaveError"]="Food Buff Overlay: 설정 저장 실패 — {0}",
                    ["FfxivPluginMissing"]="FFXIV ACT Plugin이 활성화되지 않았습니다.", ["RepositoryNotReady"]="FFXIV 데이터를 기다리는 중입니다.", ["DataApiMissing"]="FFXIV 전투 대상 데이터 API를 사용할 수 없습니다.",
                    ["UpdateAssetMissing"]="필요한 Release asset 또는 SHA-256 매니페스트가 없습니다.", ["RestartAct"]="업데이트 완료를 위해 ACT를 종료하고 다시 시작하세요.",
                    ["NetworkFailure"]="GitHub에 연결할 수 없습니다. 플러그인의 일반 기능은 계속 작동합니다.", ["InvalidPackage"]="다운로드한 업데이트 패키지가 올바르지 않습니다.",
                    ["HashFailed"]="SHA-256 검증에 실패했습니다.",
                    ["UpdateFailureGeneric"]="업데이트를 안전하게 준비할 수 없습니다.", ["DataReadFailed"]="FFXIV 데이터를 읽을 수 없습니다.", ["BrowserFailure"]="기본 브라우저를 시작할 수 없습니다."
                }
            };

        public static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return English;
            if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return Japanese;
            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return Chinese;
            if (language.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return Korean;
            return English;
        }

        public static string MapCulture(CultureInfo culture) => NormalizeLanguage(culture == null ? null : culture.Name);

        public static string FontFamily(string language)
        {
            switch (NormalizeLanguage(language))
            {
                case Japanese: return "Yu Gothic UI";
                case Chinese: return "Microsoft YaHei UI";
                case Korean: return "Malgun Gothic";
                default: return "Segoe UI";
            }
        }

        public static string Get(string language, string key, params object[] args)
        {
            Dictionary<string, string> table;
            string value;
            var normalized = NormalizeLanguage(language);
            if (!Resources.TryGetValue(normalized, out table) || !table.TryGetValue(key, out value))
                Resources[English].TryGetValue(key, out value);
            if (value == null) value = key;
            return args == null || args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
        }

        internal static IEnumerable<string> MissingUserKeys(string language)
        {
            Dictionary<string, string> table;
            Resources.TryGetValue(NormalizeLanguage(language), out table);
            return Resources[English].Keys.Where(key => key != "FallbackProbe" && (table == null || !table.ContainsKey(key)));
        }
    }
}
