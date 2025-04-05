using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using Markdig;
using Markdig.SyntaxHighlighting;
using Microsoft.VisualStudio.PlatformUI;

namespace Shine
{
    /// <summary>
    /// HTML テンプレートの生成と更新を担当するクラス（チャット履歴版）
    /// </summary>
    public class ChatHistoryDocument
    {
        // チャットメッセージを保持するリスト
        private List<string> _chatMessages;

        public MarkdownPipeline Pipeline { get; private set; }
        private readonly string _assistantIconBase64;
        private readonly Brush _foregroundBrush;

        /// <summary>
        /// 最大履歴数。0 の場合は履歴を保持しません。
        /// </summary>
        public int MaxChatHistoryCount { get; set; } = 5;

        public ChatHistoryDocument(Brush foregroundBrush, string assistantIconBase64)
        {
            _foregroundBrush = foregroundBrush;
            _assistantIconBase64 = assistantIconBase64;
            Initialize();
        }

        /// <summary>
        /// HTML の初期設定を行い、履歴を初期化する
        /// </summary>
        public void Initialize()
        {
            Pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            _chatMessages = new List<string>();
        }

        /// <summary>
        /// 履歴にチャットメッセージの HTML スニペットを追加する
        /// </summary>
        public void AppendChatMessage(string messageBlock)
        {
            if (MaxChatHistoryCount == 0)
            {
                return;
            }

            _chatMessages.Add(messageBlock);
        }

        /// <summary>
        /// 直近のメッセージにスクリプトを追加する
        /// </summary>
        public void AppendScript(string script)
        {
            if (_chatMessages.Count > 0)
            {
                _chatMessages[_chatMessages.Count - 1] += script;
            }
            else
            {
                _chatMessages.Add(script);
            }
        }

        /// <summary>
        /// 現在の HTML を生成して返す
        /// </summary>
        public string GetHtml()
        {
            StringBuilder sb = new StringBuilder();

            // ヘッダー部分の生成
            string foregroundHex = BrushHelper.ConvertBrushToHex(_foregroundBrush);
            var bgDrawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            var themedBgColor = System.Windows.Media.Color.FromArgb(bgDrawingColor.A, bgDrawingColor.R, bgDrawingColor.G, bgDrawingColor.B);
            string backgroundHex = $"#{themedBgColor.R:X2}{themedBgColor.G:X2}{themedBgColor.B:X2}";

            sb.AppendLine("<html><head><meta charset='UTF-8'>");
            sb.AppendLine("<style>");
            sb.AppendLine($"body {{ background-color: {backgroundHex}; font-family: 'Segoe UI', sans-serif; padding: 10px; color: {foregroundHex}; font-size: 12px; }}");
            sb.AppendLine(".message { margin-bottom: 10px; padding: 10px; border-radius: 8px; max-width: 80%; }");
            sb.AppendLine(".user { background-color: rgba(30,144,255,0.2); border: 1px solid DodgerBlue; text-align: left; margin-left:auto; }");
            sb.AppendLine(".assistant { background-color: rgba(34,139,34,0.2); border: 1px solid Green; text-align: left; margin-right:auto; }");
            sb.AppendLine(".error { background-color: rgba(255,0,0,0.2); border: 1px solid Red; text-align: left; margin-right:auto; }");
            sb.AppendLine(".sender { font-weight: bold; margin-bottom: 5px; }");
            sb.AppendLine("pre { padding: 10px; border-radius: 4px; overflow: auto; }");
            sb.AppendLine(".copy-button { position: absolute; top: 5px; right: 5px; padding: 2px 6px; font-size: 12px; cursor: pointer; }");
            sb.AppendLine("</style>");
            sb.AppendLine("<script>");
            sb.AppendLine("function copyToClipboard(text, button, event) {");
            sb.AppendLine("  if (event) { event.preventDefault(); event.stopPropagation(); }");
            sb.AppendLine("  if (navigator.clipboard && navigator.clipboard.writeText) {");
            sb.AppendLine("    navigator.clipboard.writeText(text).then(function() {");
            sb.AppendLine("      button.textContent = 'Copied!';");
            sb.AppendLine("      setTimeout(function() { button.textContent = 'Copy'; }, 2000);");
            sb.AppendLine("    }).catch(function(err) {");
            sb.AppendLine("      console.error('Error copying text: ', err);");
            sb.AppendLine("      fallbackCopyTextToClipboard(text, button);");
            sb.AppendLine("    });");
            sb.AppendLine("  } else {");
            sb.AppendLine("    fallbackCopyTextToClipboard(text, button);");
            sb.AppendLine("  }");
            sb.AppendLine("  return false;");
            sb.AppendLine("}");
            sb.AppendLine("function fallbackCopyTextToClipboard(text, button) {");
            sb.AppendLine("  var textArea = document.createElement('textarea');");
            sb.AppendLine("  textArea.value = text;");
            sb.AppendLine("  textArea.style.position = 'fixed';");
            sb.AppendLine("  textArea.style.left = '-9999px';");
            sb.AppendLine("  textArea.style.top = '0';");
            sb.AppendLine("  document.body.appendChild(textArea);");
            sb.AppendLine("  textArea.focus();");
            sb.AppendLine("  textArea.select();");
            sb.AppendLine("  try {");
            sb.AppendLine("    var successful = document.execCommand('copy');");
            sb.AppendLine("    button.textContent = successful ? 'Copied!' : 'Copy Failed';");
            sb.AppendLine("  } catch (err) {");
            sb.AppendLine("    console.error('Fallback: Oops, unable to copy', err);");
            sb.AppendLine("    button.textContent = 'Copy Failed';");
            sb.AppendLine("  }");
            sb.AppendLine("  document.body.removeChild(textArea);");
            sb.AppendLine("  setTimeout(function() { button.textContent = 'Copy'; }, 2000);");
            sb.AppendLine("}");
            sb.AppendLine("function scrollToBottom() {");
            sb.AppendLine("  window.scrollTo(0, document.body.scrollHeight);");
            sb.AppendLine("}");
            sb.AppendLine("function addCopyButtons() {");
            sb.AppendLine("  document.querySelectorAll('pre').forEach(function(pre) {");
            sb.AppendLine("    if (pre.parentElement.querySelector('.copy-button')) return;");
            sb.AppendLine("    pre.parentElement.style.position = 'relative';");
            sb.AppendLine("    var button = document.createElement('button');");
            sb.AppendLine("    button.className = 'copy-button';");
            sb.AppendLine("    button.textContent = 'Copy';");
            sb.AppendLine("    button.onclick = function(e) {");
            sb.AppendLine("      var code = pre.innerText;");
            sb.AppendLine("      return copyToClipboard(code, button, e);");
            sb.AppendLine("    };");
            sb.AppendLine("    pre.parentElement.appendChild(button);");
            sb.AppendLine("  });");
            sb.AppendLine("}");
            sb.AppendLine("</script>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<!-- チャットメッセージ -->");

            // 保持している各メッセージを追加
            foreach (var msg in _chatMessages)
            {
                sb.AppendLine(msg);
            }

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}