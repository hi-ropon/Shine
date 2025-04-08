using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Media;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class MentionTests
    {
        [STATestMethod]
        public void InsertMention_ReplacesAtSymbolWithInlineUI()
        {
            // RichTextBox に "Hello @Test" というテキストを設定
            System.Windows.Controls.RichTextBox richTextBox = new System.Windows.Controls.RichTextBox();
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph(new Run("Hello @Test")));
            // キャレットを文末に設定
            richTextBox.CaretPosition = richTextBox.Document.ContentEnd;

            // ダミーの Popup と ListBox を作成
            var popup = new System.Windows.Controls.Primitives.Popup();
            var listBox = new System.Windows.Controls.ListBox();

            var mention = new Mention(richTextBox, popup, listBox);
            // InsertMention により "@Test" 部分が置き換えられるはず
            mention.InsertMention("Test");

            // 置換後、テキストから "@Test" が削除されていることを確認
            TextRange tr = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            string text = tr.Text;
            Assert.IsFalse(text.Contains("@Test"));

            // InlineUIContainer が挿入されているか検証
            bool foundInlineUI = false;
            foreach (Block block in richTextBox.Document.Blocks)
            {
                if (block is Paragraph para)
                {
                    foreach (Inline inline in para.Inlines)
                    {
                        if (inline is InlineUIContainer)
                        {
                            foundInlineUI = true;
                            break;
                        }
                    }
                }
            }
            Assert.IsTrue(foundInlineUI);
        }
    }
}
