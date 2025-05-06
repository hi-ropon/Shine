using System;

namespace Shine
{
    /// <summary>
    /// VSCT の <GuidSymbol> 定義に対応する GUID 定数を保持します。
    /// </summary>
    internal static class GuidList
    {
        // パッケージ GUID
        public const string guidShinePackageString = "B1234567-89AB-CDEF-0123-456789ABCDEF";
        public static readonly Guid guidShinePackage = new Guid(guidShinePackageString);

        // コマンドセット GUID
        public const string guidShinePackageCmdSetString = "D1234567-89AB-CDEF-0123-456789ABCDEF";
        public static readonly Guid guidShinePackageCmdSet = new Guid(guidShinePackageCmdSetString);
    }

    /// <summary>
    /// VSCT の <IDSymbol> 定義に対応するコマンド ID 定数を保持します。
    /// 名前はコード規約に合わせ、先頭を小文字にしています。
    /// </summary>
    internal static class PkgCmdIDList
    {
        // メニューグループ ID
        public const uint myMenuGroup = 0x1020;
        // Shine(Code Assistant Tool) メニュー起動コマンド
        public const uint showAiChatCommand = 0x0100;
        // Alt+@ で補完トリガーするコマンド
        public const uint triggerSuggestionCommand = 0x0101;
    }
}
