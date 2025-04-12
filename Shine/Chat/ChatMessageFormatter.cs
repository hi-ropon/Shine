using System;
using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace Shine
{
    /// <summary>
    /// チャットメッセージのフォーマットを行うクラス
    /// </summary>
    public static class ChatMessageFormatter
    {
        /// <summary>
        /// 送信者名とメッセージ本文から、Markdown の変換や装飾を行い HTML スニペットを生成する
        /// </summary>
        public static string FormatMessage(string senderName, string message, MarkdownPipeline pipeline, string assistantIconBase64)
        {
            string htmlMessage;
            try
            {
                htmlMessage = Markdown.ToHtml(message, pipeline);
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

            string iconTag = "";
            if (cssClass == "assistant" && !string.IsNullOrEmpty(assistantIconBase64))
            {
                iconTag = $"<img src=\"data:image/png;base64,{assistantIconBase64}\" " +
                          $"alt=\"icon\" style=\"width:32px;height:32px;margin-right:5px;vertical-align:middle;\" />";
            }

            string messageBlock =
                $"<div class='message {cssClass}'>" +
                $"<div class='sender'>{iconTag}{WebUtility.HtmlEncode(senderName)}</div>" +
                $"{htmlMessage}" +
                $"</div>";

            return messageBlock;
        }
    }
}