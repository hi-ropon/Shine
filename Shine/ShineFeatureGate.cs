// ========================
// ファイル名: ShineFeatureGate.cs
// 説明    : Shine の機能排他制御フラグ
// ========================
namespace Shine
{
    /// <summary>機能間の排他制御を行うグローバルゲート。</summary>
    internal static class ShineFeatureGate
    {
        public static bool IsInlineChatActive { get; private set; }
        public static bool IsSuggestionRunning { get; private set; }

        public static bool TryBeginInlineChat()
        {
            if (IsSuggestionRunning || IsInlineChatActive)
                return false;
            IsInlineChatActive = true;
            return true;
        }

        public static void EndInlineChat() => IsInlineChatActive = false;

        public static bool TryBeginSuggestion()
        {
            if (IsInlineChatActive || IsSuggestionRunning)
                return false;
            IsSuggestionRunning = true;
            return true;
        }

        public static void EndSuggestion() => IsSuggestionRunning = false;
    }
}
