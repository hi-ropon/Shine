using System.Text;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using Markdig;
using Markdig.SyntaxHighlighting;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows;

namespace Shine
{
    /// <summary>
    /// HTML テンプレートの生成と更新を担当するクラス
    /// </summary>
    public class ChatHistoryDocument
    {
        private readonly StringBuilder _htmlContent;
        public MarkdownPipeline Pipeline { get; private set; }
        private readonly string _assistantIconBase64;
        private readonly Brush _foregroundBrush;

        public ChatHistoryDocument(Brush foregroundBrush, string assistantIconBase64)
        {
            _foregroundBrush = foregroundBrush;
            _assistantIconBase64 = assistantIconBase64;
            _htmlContent = new StringBuilder();
            Initialize();
        }

        /// <summary>
        /// 初期 HTML コンテンツをセットアップする
        /// </summary>
        public void Initialize()
        {
            _htmlContent.Clear();
            Pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            string foregroundHex = Shine.BrushHelper.ConvertBrushToHex(_foregroundBrush);

            var bgDrawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            var themedBgColor = Color.FromArgb(bgDrawingColor.A, bgDrawingColor.R, bgDrawingColor.G, bgDrawingColor.B);
            string backgroundHex = $"#{themedBgColor.R:X2}{themedBgColor.G:X2}{themedBgColor.B:X2}";

            _htmlContent.AppendLine("<html><head><meta charset='UTF-8'>");
            _htmlContent.AppendLine("<style>");
            _htmlContent.AppendLine(
                $"body {{ " +
                $"background-color: {backgroundHex}; " +
                $"font-family: 'Segoe UI', sans-serif; " +
                $"padding: 10px; " +
                $"color: {foregroundHex}; " +
                $"font-size: 12px; " +
                $"}}"
            );
            _htmlContent.AppendLine(".message { margin-bottom: 10px; padding: 10px; border-radius: 8px; max-width: 80%; }");
            _htmlContent.AppendLine(".user { background-color: rgba(30,144,255,0.2); border: 1px solid DodgerBlue; text-align: left; margin-left:auto; }");
            _htmlContent.AppendLine(".assistant { background-color: rgba(34,139,34,0.2); border: 1px solid Green; text-align: left; margin-right:auto; }");
            _htmlContent.AppendLine(".error { background-color: rgba(255,0,0,0.2); border: 1px solid Red; text-align: left; margin-right:auto; }");
            _htmlContent.AppendLine(".sender { font-weight: bold; margin-bottom: 5px; }");
            _htmlContent.AppendLine("pre { padding: 10px; border-radius: 4px; overflow: auto; }");

            // コピーボタン用スタイル
            _htmlContent.AppendLine(".copy-button { position: absolute; top: 5px; right: 5px; padding: 2px 6px; font-size: 12px; cursor: pointer; }");
            _htmlContent.AppendLine("</style>");

            _htmlContent.AppendLine("<script>");
            _htmlContent.AppendLine("function copyToClipboard(text, button, event) {");
            _htmlContent.AppendLine("  if (event) { event.preventDefault(); event.stopPropagation(); }");
            _htmlContent.AppendLine("  if (navigator.clipboard && navigator.clipboard.writeText) {");
            _htmlContent.AppendLine("    navigator.clipboard.writeText(text).then(function() {");
            _htmlContent.AppendLine("      button.textContent = 'Copied!';");
            _htmlContent.AppendLine("      setTimeout(function() { button.textContent = 'Copy'; }, 2000);");
            _htmlContent.AppendLine("    }).catch(function(err) {");
            _htmlContent.AppendLine("      console.error('Error copying text: ', err);");
            _htmlContent.AppendLine("      fallbackCopyTextToClipboard(text, button);");
            _htmlContent.AppendLine("    });");
            _htmlContent.AppendLine("  } else {");
            _htmlContent.AppendLine("    fallbackCopyTextToClipboard(text, button);");
            _htmlContent.AppendLine("  }");
            _htmlContent.AppendLine("  return false;");
            _htmlContent.AppendLine("}");
            _htmlContent.AppendLine("");
            _htmlContent.AppendLine("function fallbackCopyTextToClipboard(text, button) {");
            _htmlContent.AppendLine("  var textArea = document.createElement('textarea');");
            _htmlContent.AppendLine("  textArea.value = text;");
            _htmlContent.AppendLine("  textArea.style.position = 'fixed';");
            _htmlContent.AppendLine("  textArea.style.left = '-9999px';");
            _htmlContent.AppendLine("  textArea.style.top = '0';");
            _htmlContent.AppendLine("  document.body.appendChild(textArea);");
            _htmlContent.AppendLine("  textArea.focus();");
            _htmlContent.AppendLine("  textArea.select();");
            _htmlContent.AppendLine("  try {");
            _htmlContent.AppendLine("    var successful = document.execCommand('copy');");
            _htmlContent.AppendLine("    button.textContent = successful ? 'Copied!' : 'Copy Failed';");
            _htmlContent.AppendLine("  } catch (err) {");
            _htmlContent.AppendLine("    console.error('Fallback: Oops, unable to copy', err);");
            _htmlContent.AppendLine("    button.textContent = 'Copy Failed';");
            _htmlContent.AppendLine("  }");
            _htmlContent.AppendLine("  document.body.removeChild(textArea);");
            _htmlContent.AppendLine("  setTimeout(function() { button.textContent = 'Copy'; }, 2000);");
            _htmlContent.AppendLine("}");
            _htmlContent.AppendLine("");
            _htmlContent.AppendLine("function scrollToBottom() {");
            _htmlContent.AppendLine("  window.scrollTo(0, document.body.scrollHeight);");
            _htmlContent.AppendLine("}");
            _htmlContent.AppendLine("");
            _htmlContent.AppendLine("function addCopyButtons() {");
            _htmlContent.AppendLine("  document.querySelectorAll('pre').forEach(function(pre) {");
            _htmlContent.AppendLine("    if (pre.parentElement.querySelector('.copy-button')) return;");
            _htmlContent.AppendLine("    pre.parentElement.style.position = 'relative';");
            _htmlContent.AppendLine("    var button = document.createElement('button');");
            _htmlContent.AppendLine("    button.className = 'copy-button';");
            _htmlContent.AppendLine("    button.textContent = 'Copy';");
            _htmlContent.AppendLine("    button.onclick = function(e) {");
            _htmlContent.AppendLine("      var code = pre.innerText;");
            _htmlContent.AppendLine("      return copyToClipboard(code, button, e);");
            _htmlContent.AppendLine("    };");
            _htmlContent.AppendLine("    pre.parentElement.appendChild(button);");
            _htmlContent.AppendLine("  });");
            _htmlContent.AppendLine("}");
            _htmlContent.AppendLine("</script>");
            _htmlContent.AppendLine("</head><body>");
            _htmlContent.AppendLine("<!-- チャットメッセージ -->");
            _htmlContent.AppendLine("</body></html>");
        }

        /// <summary>
        /// チャットメッセージの HTML スニペットを本文内に追加する
        /// </summary>
        public void AppendChatMessage(string messageBlock)
        {
            int bodyCloseIndex = _htmlContent.ToString().LastIndexOf("</body>");
            if (bodyCloseIndex >= 0)
            {
                _htmlContent.Insert(bodyCloseIndex, messageBlock);
            }
            else
            {
                _htmlContent.AppendLine(messageBlock);
            }
        }

        /// <summary>
        /// スクリプトを本文内に挿入する
        /// </summary>
        public void AppendScript(string script)
        {
            int bodyCloseIndex = _htmlContent.ToString().LastIndexOf("</body>");
            if (bodyCloseIndex >= 0)
            {
                _htmlContent.Insert(bodyCloseIndex, script);
            }
        }

        /// <summary>
        /// 現在の HTML を文字列として返す
        /// </summary>
        public string GetHtml()
        {
            return _htmlContent.ToString();
        }
    }
}