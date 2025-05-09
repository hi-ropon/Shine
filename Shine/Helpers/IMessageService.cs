using System;
using System.Windows.Forms;

namespace Shine.Helpers
{
    /// <summary>
    /// メッセージサービス
    /// </summary>
    public interface IMessageService
    {
        void OKOnly(string message);
        DialogResult QuestionOKCancel(string message);

        /// <summary>
        /// catch 節で例外を表示する
        /// </summary>
        /// <param name="ex">発生した例外</param>
        /// <param name="contextMessage">任意のコンテキストメッセージ</param>
        void ShowError(Exception ex, string contextMessage = null);
    }
}
