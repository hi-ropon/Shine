// プロジェクト名: ShineExtensionTests

using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;  // WindowsForms 用
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
        // AsyncPackage の GetServiceAsync を隠蔽するフェイククラス
        private class FakeAsyncPackage : AsyncPackage
        {
            public object FakeService { get; set; }
            // base の GetServiceAsync は override できないため new キーワードで隠蔽
            public new Task<object> GetServiceAsync(Type serviceType)
            {
                return Task.FromResult(FakeService);
            }
        }

        [TestMethod]
        public async Task InitializeAsync_コマンドが追加されること()
        {
            // Arrange: IMenuCommandService のモックを作成
            var mockMenuCommandService = new Mock<IMenuCommandService>();
            var fakePackage = new FakeAsyncPackage { FakeService = mockMenuCommandService.Object };

            // Act: ShowAiChatCommand.InitializeAsync を呼び出す
            await ShowAiChatCommand.InitializeAsync(fakePackage);

            // Assert: AddCommand が呼ばれていることを検証
            mockMenuCommandService.Verify(m => m.AddCommand(It.IsAny<MenuCommand>()), Times.AtLeastOnce());
        }
    }

    [TestClass]
    public class TestModelNameEditor
    {
        // IWindowsFormsEditorService のフェイク実装（WindowsForms の Control を使用）
        private class FakeEditorService : IWindowsFormsEditorService
        {
            public void CloseDropDown() { }
            public DialogResult ShowDialog(Form dialog) => DialogResult.OK;
            public void DropDownControl(Control control)
            {
                // WindowsForms の ListBox なら先頭項目を選択するシミュレーション
                if (control is System.Windows.Forms.ListBox lb && lb.Items.Count > 0)
                {
                    lb.SelectedIndex = 0;
                }
            }
        }

        // IServiceProvider のフェイク実装
        private class FakeServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IWindowsFormsEditorService))
                    return new FakeEditorService();
                return null;
            }
        }

        // ITypeDescriptorContext のフェイク実装
        private class FakeTypeDescriptorContext : ITypeDescriptorContext
        {
            public object Instance { get; set; }
            public IContainer Container => null;
            // 必須メンバー PropertyDescriptor を実装（今回は null でOK）
            public PropertyDescriptor PropertyDescriptor => null;
            public object GetService(Type serviceType) => null;
            public void OnComponentChanged() { }
            public bool OnComponentChanging() => true;
        }

        [TestMethod]
        public void GetEditStyle_DropDownが返されること()
        {
            // Arrange
            var editor = new ModelNameEditor();

            // Act
            var style = editor.GetEditStyle(null);

            // Assert
            Assert.AreEqual(System.Drawing.Design.UITypeEditorEditStyle.DropDown, style);
        }

        [TestMethod]
        public void EditValue_選択結果が返されること()
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
            // OpenAI の場合、項目は "gpt-4o-mini", "gpt-4o", "o1-mini", "o3-mini" の順になっているので先頭が返るはず
            Assert.AreEqual("gpt-4o-mini", result);
        }
    }

    [TestClass]
    public class TestChatHistoryDocument
    {
        [TestMethod]
        public void AppendChatMessageとAppendScript_GetHtmlで内容が反映されること()
        {
            // Arrange
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black);
            string dummyIcon = "dummyIconBase64";
            var doc = new ChatHistoryDocument(brush, dummyIcon);
            string message1 = "テストメッセージ1";
            string message2 = "テストメッセージ2";
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
            // HTML のヘッダーが含まれているか確認
            Assert.IsTrue(html.Contains("<html>") && html.Contains("</html>"));
        }
    }

    [TestClass]
    public class TestChatMessageFormatter
    {
        [TestMethod]
        public void FormatMessage_USERの場合ユーザークラスが設定されること()
        {
            // Arrange
            string sender = "USER";
            string message = "これはテストです。";
            // Markdig のバージョンに合わせ、UseSyntaxHighlighting() を削除
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
        public void FormatMessage_Assistantの場合アイコンが含まれること()
        {
            // Arrange
            string sender = "Assistant";
            string message = "アシスタントのテストです。";
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
        public void ConvertBrushToHex_正しい16進数文字列を返すこと()
        {
            // Arrange: オレンジ色 (RGB: 255, 128, 0)
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
        public void IsCodeFile_有効な拡張子の場合はtrueを返すこと()
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
        public void IsCodeFile_無効な拡張子の場合はfalseを返すこと()
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
        public void InsertMention_メンションが正しく挿入されること()
        {
            // Arrange: RichTextBox を生成し、"Hello @" のテキストを設定
            var richTextBox = new System.Windows.Controls.RichTextBox();
            richTextBox.Document.Blocks.Clear();
            var paragraph = new System.Windows.Documents.Paragraph();
            paragraph.Inlines.Add(new System.Windows.Documents.Run("Hello @"));
            richTextBox.Document.Blocks.Add(paragraph);
            // キャレット位置をドキュメント末尾に設定
            richTextBox.CaretPosition = richTextBox.Document.ContentEnd;

            var popup = new System.Windows.Controls.Primitives.Popup();
            var listBox = new System.Windows.Controls.ListBox();
            var mention = new Mention(richTextBox, popup, listBox);

            // Act: メンションを挿入
            mention.InsertMention("test.txt");

            // Assert: ドキュメント内に InlineUIContainer が存在し、
            // その中の Border 内に TextBlock が "@test.txt" と表示されているか確認
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
            Assert.IsTrue(foundInlineUI, "メンションが挿入されていません。");
        }
    }
}
