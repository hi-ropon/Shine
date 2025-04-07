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
            // RichTextBox �� "Hello @Test" �Ƃ����e�L�X�g��ݒ�
            System.Windows.Controls.RichTextBox richTextBox = new System.Windows.Controls.RichTextBox();
            richTextBox.Document.Blocks.Clear();
            richTextBox.Document.Blocks.Add(new Paragraph(new Run("Hello @Test")));
            // �L�����b�g�𕶖��ɐݒ�
            richTextBox.CaretPosition = richTextBox.Document.ContentEnd;

            // �_�~�[�� Popup �� ListBox ���쐬
            var popup = new System.Windows.Controls.Primitives.Popup();
            var listBox = new System.Windows.Controls.ListBox();

            var mention = new Mention(richTextBox, popup, listBox);
            // InsertMention �ɂ�� "@Test" �������u����������͂�
            mention.InsertMention("Test");

            // �u����A�e�L�X�g���� "@Test" ���폜����Ă��邱�Ƃ��m�F
            TextRange tr = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            string text = tr.Text;
            Assert.IsFalse(text.Contains("@Test"));

            // InlineUIContainer ���}������Ă��邩����
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
