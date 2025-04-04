using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Wpf;
using Markdig;
using System.Net;
using System.Windows.Media;
using Markdig.SyntaxHighlighting;
using Microsoft.VisualStudio.PlatformUI;

namespace Shine
{
    /// <summary>
    /// WebView2 を利用してチャット履歴を HTML 表示する
    /// </summary>
    public class ChatHistory
    {
        private readonly WebView2 _chatHistoryWebView;
        private readonly StringBuilder _htmlContent;
        private Brush _foregroundBrush;
        private MarkdownPipeline _pipeline;

        public ChatHistory(WebView2 chatHistoryWebView, Brush foregroundBrush)
        {
            _chatHistoryWebView = chatHistoryWebView ?? throw new ArgumentNullException(nameof(chatHistoryWebView));
            _foregroundBrush = foregroundBrush ?? throw new ArgumentNullException(nameof(foregroundBrush));
            _htmlContent = new StringBuilder();
            InitializeHtml();
        }

        /// <summary>
        /// 初期 HTML コンテンツをセットアップする
        /// </summary>
        private void InitializeHtml()
        {
            _htmlContent.Clear();

            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseSyntaxHighlighting()
                .Build();

            // ForegroundBrush の色を16進数文字列に変換
            string foregroundHex = ConvertBrushToHex(_foregroundBrush);

            // VS の背景色を取得して16進数文字列に変換
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
            
            // コピーボタン　スタイル
            _htmlContent.AppendLine(".copy-button { position: absolute; top: 5px; right: 5px; padding: 2px 6px; font-size: 12px; cursor: pointer; }");
            _htmlContent.AppendLine("</style>");

            // コピーボタン　スクリプト
            _htmlContent.AppendLine("<script>");
            _htmlContent.AppendLine("function copyToClipboard(text, button) {");
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
            _htmlContent.AppendLine("}");
            _htmlContent.AppendLine("");
            _htmlContent.AppendLine("function fallbackCopyTextToClipboard(text, button) {");
            _htmlContent.AppendLine("  var textArea = document.createElement('textarea');");
            _htmlContent.AppendLine("  textArea.value = text;");
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
            _htmlContent.AppendLine("function addCopyButtons() {");
            _htmlContent.AppendLine("  document.querySelectorAll('pre').forEach(function(pre) {");
            _htmlContent.AppendLine("    if (pre.parentElement.querySelector('.copy-button')) return;");
            _htmlContent.AppendLine("    pre.parentElement.style.position = 'relative';");
            _htmlContent.AppendLine("    var button = document.createElement('button');");
            _htmlContent.AppendLine("    button.className = 'copy-button';");
            _htmlContent.AppendLine("    button.textContent = 'Copy';");
            _htmlContent.AppendLine("    button.onclick = function() {");
            _htmlContent.AppendLine("      var code = pre.innerText;");
            _htmlContent.AppendLine("      copyToClipboard(code, button);");
            _htmlContent.AppendLine("    };");
            _htmlContent.AppendLine("    pre.parentElement.appendChild(button);");
            _htmlContent.AppendLine("  });");
            _htmlContent.AppendLine("}");
            _htmlContent.AppendLine("</script>");
            _htmlContent.AppendLine("</head><body>");
            _htmlContent.AppendLine("<!-- チャットメッセージ -->");
            _htmlContent.AppendLine("</body></html>");

            // 初期状態の HTML を WebView2 に読み込む
            _chatHistoryWebView.NavigateToString(_htmlContent.ToString());
        }

        /// <summary>
        /// チャットメッセージを追加し、HTML を更新する
        /// </summary>
        public void AddChatMessage(string senderName, string message)
        {
            string htmlMessage;
            try
            {
                htmlMessage = Markdown.ToHtml(message, _pipeline);
                htmlMessage = Regex.Replace(htmlMessage, @"background-color:\s*White;", "background-color:GhostWhite;", RegexOptions.IgnoreCase);
            }
            catch (Exception)
            {
                htmlMessage = WebUtility.HtmlEncode(message);
            }

            string cssClass = "assistant";
            if (senderName.Equals("USER", StringComparison.OrdinalIgnoreCase))
            {
                cssClass = "user";
            }
            else if (senderName.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                cssClass = "error";
            }

            string messageBlock = $"<div class='message {cssClass}'>" +
                                  $"<div class='sender'>{WebUtility.HtmlEncode(senderName)}</div>" +
                                  $"{htmlMessage}" +
                                  $"</div>";

            int bodyCloseIndex = _htmlContent.ToString().LastIndexOf("</body>");
            if (bodyCloseIndex >= 0)
            {
                _htmlContent.Insert(bodyCloseIndex, messageBlock);
            }
            else
            {
                _htmlContent.AppendLine(messageBlock);
            }

            string script = "<script>addCopyButtons(); window.scrollTo(0, document.body.scrollHeight);</script>";
            bodyCloseIndex = _htmlContent.ToString().LastIndexOf("</body>");
            if (bodyCloseIndex >= 0)
            {
                _htmlContent.Insert(bodyCloseIndex, script);
            }

            _chatHistoryWebView.NavigateToString(_htmlContent.ToString());
        }

        /// <summary>
        /// チャット履歴をクリアする
        /// </summary>
        public void ClearHistory()
        {
            InitializeHtml();
        }

        /// <summary>
        /// Brush を 16進数文字列 (例: #RRGGBB) に変換する
        /// </summary>
        private string ConvertBrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush solidColorBrush)
            {
                var color = solidColorBrush.Color;
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#000000"; // 変換できない場合は黒
        }

        /// <summary>
        /// テーマ変更時に新しい ForegroundBrush を反映する（body の文字色を更新）
        /// </summary>
        public async System.Threading.Tasks.Task UpdateForegroundColorAsync(Brush newForegroundBrush)
        {
            if (newForegroundBrush == null) return;
            _foregroundBrush = newForegroundBrush;
            string newColorHex = ConvertBrushToHex(newForegroundBrush);
            string script = $"document.body.style.color = '{newColorHex}';";
            await _chatHistoryWebView.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// VS の背景色を取得して、WebView2 の背景色を更新する
        /// </summary>
        public async System.Threading.Tasks.Task UpdateBackgroundColorAsync()
        {
            // VS の背景色を取得して16進数文字列に変換
            var bgDrawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            var themedBgColor = Color.FromArgb(bgDrawingColor.A, bgDrawingColor.R, bgDrawingColor.G, bgDrawingColor.B);
            string backgroundHex = $"#{themedBgColor.R:X2}{themedBgColor.G:X2}{themedBgColor.B:X2}";

            // document.body の backgroundColor を更新するスクリプトを実行
            string script = $"document.body.style.backgroundColor = '{backgroundHex}';";
            await _chatHistoryWebView.ExecuteScriptAsync(script);
        }
    }
}
