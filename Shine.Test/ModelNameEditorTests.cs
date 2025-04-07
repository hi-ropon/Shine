// ファイル名: ModelNameEditorTests.cs
using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    // FakeWindowsFormsEditorService の修正版
    public class FakeWindowsFormsEditorService : IWindowsFormsEditorService
    {
        public void CloseDropDown() { }

        public void DropDownControl(System.Windows.Forms.Control control)
        {
            // ListBox が渡された場合、最初の項目を自動選択する例
            if (control is ListBox listBox)
            {
                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }
            }
        }

        // ShowDialog(Form) の実装（テスト用に単に OK を返す）
        public DialogResult ShowDialog(Form dialog)
        {
            return DialogResult.OK;
        }
    }

    // FakeServiceProvider はそのままで OK
    public class FakeServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IWindowsFormsEditorService))
                return new FakeWindowsFormsEditorService();
            return null;
        }
    }

    // FakeTypeDescriptorContext の修正版
    public class FakeTypeDescriptorContext : ITypeDescriptorContext
    {
        public object Instance { get; set; }

        public PropertyDescriptor PropertyDescriptor => null;

        // Container プロパティの実装（ここでは null を返す）
        public IContainer Container => null;

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IWindowsFormsEditorService))
                return new FakeWindowsFormsEditorService();
            return null;
        }

        public bool OnComponentChanging() => true;

        public void OnComponentChanged() { }
    }

    [TestClass]
    public class ModelNameEditorTests
    {
        [TestMethod]
        public void EditValue_ReturnsFirstModel_ForOpenAI()
        {
            // Arrange
            AiAssistantOptions options = new AiAssistantOptions
            {
                Provider = OpenAiProvider.OpenAI,
                OpenAIModelName = "gpt-4o-mini"
            };
            var context = new FakeTypeDescriptorContext { Instance = options };
            var provider = new FakeServiceProvider();
            ModelNameEditor editor = new ModelNameEditor();

            // Act
            var result = editor.EditValue(context, provider, "initial");

            // Assert
            // OpenAI の場合、"gpt-4o-mini", "gpt-4o", "o1-mini", "o3-mini" の中から
            // ドロップダウンの処理で最初の項目 "gpt-4o-mini" が選択される想定
            Assert.IsNotNull(result, "結果が null でないこと");
            Assert.AreEqual("gpt-4o-mini", result.ToString(), "OpenAIの場合、最初のモデルが選択されること");
        }

        [TestMethod]
        public void EditValue_ReturnsFirstModel_ForAzureOpenAI()
        {
            // Arrange
            AiAssistantOptions options = new AiAssistantOptions
            {
                Provider = OpenAiProvider.AzureOpenAI,
                AzureDeploymentName = "dummy"
            };
            var context = new FakeTypeDescriptorContext { Instance = options };
            var provider = new FakeServiceProvider();
            ModelNameEditor editor = new ModelNameEditor();

            // Act
            var result = editor.EditValue(context, provider, "initial");

            // Assert
            // AzureOpenAI の場合、"gpt-4o-mini", "gpt-4o", "o1-mini" の中から
            // ドロップダウンの処理で最初の項目 "gpt-4o-mini" が選択される想定
            Assert.IsNotNull(result, "結果が null でないこと");
            Assert.AreEqual("gpt-4o-mini", result.ToString(), "AzureOpenAIの場合、最初のモデルが選択されること");
        }
    }
}
