// �v���W�F�N�g��: ShineExtensionTests

using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;  // WindowsForms �p
using System.Windows.Forms.Design;
using Markdig;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shine;
using Color = System.Drawing.Color;

namespace Shine.Tests
{
    [TestClass]
    public class TestShowAiChatCommand
    {
        // AsyncPackage �� GetServiceAsync ���B������t�F�C�N�N���X
        private class FakeAsyncPackage : AsyncPackage
        {
            public object FakeService { get; set; }
            // base �� GetServiceAsync �� override �ł��Ȃ����� new �L�[���[�h�ŉB��
            public new Task<object> GetServiceAsync(Type serviceType)
            {
                return Task.FromResult(FakeService);
            }
        }

        [TestMethod]
        public async Task InitializeAsync_�R�}���h���ǉ�����邱��()
        {
            // Arrange: IMenuCommandService �̃��b�N���쐬
            var mockMenuCommandService = new Mock<IMenuCommandService>();
            var fakePackage = new FakeAsyncPackage { FakeService = mockMenuCommandService.Object };

            // Act: ShowAiChatCommand.InitializeAsync ���Ăяo��
            await ShowAiChatCommand.InitializeAsync(fakePackage);

            // Assert: AddCommand ���Ă΂�Ă��邱�Ƃ�����
            mockMenuCommandService.Verify(m => m.AddCommand(It.IsAny<MenuCommand>()), Times.AtLeastOnce());
        }
    }

    [TestClass]
    public class TestModelNameEditor
    {
        // IWindowsFormsEditorService �̃t�F�C�N�����iWindowsForms �� Control ���g�p�j
        private class FakeEditorService : IWindowsFormsEditorService
        {
            public void CloseDropDown() { }
            public DialogResult ShowDialog(Form dialog) => DialogResult.OK;
            public void DropDownControl(Control control)
            {
                // WindowsForms �� ListBox �Ȃ�擪���ڂ�I������V�~�����[�V����
                if (control is System.Windows.Forms.ListBox lb && lb.Items.Count > 0)
                {
                    lb.SelectedIndex = 0;
                }
            }
        }

        // IServiceProvider �̃t�F�C�N����
        private class FakeServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IWindowsFormsEditorService))
                    return new FakeEditorService();
                return null;
            }
        }

        // ITypeDescriptorContext �̃t�F�C�N����
        private class FakeTypeDescriptorContext : ITypeDescriptorContext
        {
            public object Instance { get; set; }
            public IContainer Container => null;
            // �K�{�����o�[ PropertyDescriptor �������i����� null ��OK�j
            public PropertyDescriptor PropertyDescriptor => null;
            public object GetService(Type serviceType) => null;
            public void OnComponentChanged() { }
            public bool OnComponentChanging() => true;
        }

        [TestMethod]
        public void GetEditStyle_DropDown���Ԃ���邱��()
        {
            // Arrange
            var editor = new ModelNameEditor();

            // Act
            var style = editor.GetEditStyle(null);

            // Assert
            Assert.AreEqual(System.Drawing.Design.UITypeEditorEditStyle.DropDown, style);
        }

        [TestMethod]
        public void EditValue_�I�����ʂ��Ԃ���邱��()
        {
            // Arrange
            var options = new AiAssistantOptions { Provider = OpenAiProvider.OpenAI };
            var context = new FakeTypeDescriptorContext { Instance = options };
            var provider = new FakeServiceProvider();
            var editor = new ModelNameEditor();
            string initialValue = null;

            // Act
            var result = editor.EditValue(context, provider, initialValue);

            // Assert:
            // OpenAI �̏ꍇ�A���ڂ� "gpt-4o-mini", "gpt-4o", "o1-mini", "o3-mini" �̏��ɂȂ��Ă���̂Ő擪���Ԃ�͂�
            Assert.AreEqual("gpt-4o-mini", result);
        }
    }

    [TestClass]
    public class TestChatHistoryDocument
    {
        [TestMethod]
        public void AppendChatMessage��AppendScript_GetHtml�œ��e�����f����邱��()
        {
            // Arrange
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
            string dummyIcon = "dummyIconBase64";
            var doc = new ChatHistoryDocument(brush, dummyIcon);
            string message1 = "�e�X�g���b�Z�[�W1";
            string message2 = "�e�X�g���b�Z�[�W2";
            string script = "<script>console.log('test');</script>";

            // Act
            doc.AppendChatMessage(message1);
            doc.AppendChatMessage(message2);
            doc.AppendScript(script);
            string html = doc.GetHtml();

            // Assert
            Assert.IsTrue(html.Contains(message1));
            Assert.IsTrue(html.Contains(message2));
            Assert.IsTrue(html.Contains("console.log('test')"));
            // HTML �̃w�b�_�[���܂܂�Ă��邩�m�F
            Assert.IsTrue(html.Contains("<html>") && html.Contains("</html>"));
        }
    }

    [TestClass]
    public class TestChatMessageFormatter
    {
        [TestMethod]
        public void FormatMessage_USER�̏ꍇ���[�U�[�N���X���ݒ肳��邱��()
        {
            // Arrange
            string sender = "USER";
            string message = "����̓e�X�g�ł��B";
            // Markdig �̃o�[�W�����ɍ��킹�AUseSyntaxHighlighting() ���폜
            var pipeline = new MarkdownPipelineBuilder()
                            .UseAdvancedExtensions()
                            .Build();
            string dummyIcon = "dummyIconBase64";

            // Act
            string result = ChatMessageFormatter.FormatMessage(sender, message, pipeline, dummyIcon);

            // Assert
            Assert.IsTrue(result.Contains("class='message user'") || result.Contains("message user"));
            Assert.IsTrue(result.Contains(System.Net.WebUtility.HtmlEncode(message)));
        }

        [TestMethod]
        public void FormatMessage_Assistant�̏ꍇ�A�C�R�����܂܂�邱��()
        {
            // Arrange
            string sender = "Assistant";
            string message = "�A�V�X�^���g�̃e�X�g�ł��B";
            var pipeline = new MarkdownPipelineBuilder()
                            .UseAdvancedExtensions()
                            .Build();
            string dummyIcon = "dummyIconBase64";

            // Act
            string result = ChatMessageFormatter.FormatMessage(sender, message, pipeline, dummyIcon);

            // Assert
            Assert.IsTrue(result.Contains("class='message assistant'") || result.Contains("message assistant"));
            Assert.IsTrue(result.Contains(dummyIcon));
        }
    }

    [TestClass]
    public class TestBrushHelper
    {
        [TestMethod]
        public void ConvertBrushToHex_������16�i���������Ԃ�����()
        {
            // Arrange: �I�����W�F (RGB: 255, 128, 0)
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 128, 0));
            // Act
            string hex = BrushHelper.ConvertBrushToHex(brush);
            // Assert
            Assert.AreEqual("#FF8000", hex);
        }
    }

    [TestClass]
    public class TestCodeFile
    {
        [TestMethod]
        public void IsCodeFile_�L���Ȋg���q�̏ꍇ��true��Ԃ�����()
        {
            Assert.IsTrue(CodeFile.IsCodeFile("test.cs"));
            Assert.IsTrue(CodeFile.IsCodeFile("example.vb"));
            Assert.IsTrue(CodeFile.IsCodeFile("sample.cpp"));
            Assert.IsTrue(CodeFile.IsCodeFile("header.h"));
            Assert.IsTrue(CodeFile.IsCodeFile("data.csv"));
            Assert.IsTrue(CodeFile.IsCodeFile("config.xml"));
            Assert.IsTrue(CodeFile.IsCodeFile("file.json"));
        }

        [TestMethod]
        public void IsCodeFile_�����Ȋg���q�̏ꍇ��false��Ԃ�����()
        {
            Assert.IsFalse(CodeFile.IsCodeFile("document.txt"));
            Assert.IsFalse(CodeFile.IsCodeFile("image.png"));
            Assert.IsFalse(CodeFile.IsCodeFile("archive.zip"));
        }
    }

    [TestClass]
    public class TestMention
    {
        [TestMethod]
        public void InsertMention_�����V�������������}������邱��()
        {
            // Arrange: RichTextBox �𐶐����A"Hello @" �̃e�L�X�g��ݒ�
            var richTextBox = new System.Windows.Controls.RichTextBox();
            richTextBox.Document.Blocks.Clear();
            var paragraph = new System.Windows.Documents.Paragraph();
            paragraph.Inlines.Add(new System.Windows.Documents.Run("Hello @"));
            richTextBox.Document.Blocks.Add(paragraph);
            // �L�����b�g�ʒu���h�L�������g�����ɐݒ�
            richTextBox.CaretPosition = richTextBox.Document.ContentEnd;

            var popup = new System.Windows.Controls.Primitives.Popup();
            var listBox = new System.Windows.Controls.ListBox();
            var mention = new Mention(richTextBox, popup, listBox);

            // Act: �����V������}��
            mention.InsertMention("test.txt");

            // Assert: �h�L�������g���� InlineUIContainer �����݂��A
            // ���̒��� Border ���� TextBlock �� "@test.txt" �ƕ\������Ă��邩�m�F
            bool foundInlineUI = false;
            foreach (var block in richTextBox.Document.Blocks)
            {
                if (block is System.Windows.Documents.Paragraph para)
                {
                    foreach (var inline in para.Inlines)
                    {
                        if (inline is System.Windows.Documents.InlineUIContainer ui)
                        {
                            if (ui.Child is System.Windows.Controls.Border border &&
                                border.Child is System.Windows.Controls.TextBlock tb)
                            {
                                if (tb.Text == "@test.txt")
                                {
                                    foundInlineUI = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            Assert.IsTrue(foundInlineUI, "�����V�������}������Ă��܂���B");
        }
    }
}
