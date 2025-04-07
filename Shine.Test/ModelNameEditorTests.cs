// �t�@�C����: ModelNameEditorTests.cs
using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    // FakeWindowsFormsEditorService �̏C����
    public class FakeWindowsFormsEditorService : IWindowsFormsEditorService
    {
        public void CloseDropDown() { }

        public void DropDownControl(System.Windows.Forms.Control control)
        {
            // ListBox ���n���ꂽ�ꍇ�A�ŏ��̍��ڂ������I�������
            if (control is ListBox listBox)
            {
                if (listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }
            }
        }

        // ShowDialog(Form) �̎����i�e�X�g�p�ɒP�� OK ��Ԃ��j
        public DialogResult ShowDialog(Form dialog)
        {
            return DialogResult.OK;
        }
    }

    // FakeServiceProvider �͂��̂܂܂� OK
    public class FakeServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IWindowsFormsEditorService))
                return new FakeWindowsFormsEditorService();
            return null;
        }
    }

    // FakeTypeDescriptorContext �̏C����
    public class FakeTypeDescriptorContext : ITypeDescriptorContext
    {
        public object Instance { get; set; }

        public PropertyDescriptor PropertyDescriptor => null;

        // Container �v���p�e�B�̎����i�����ł� null ��Ԃ��j
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
            // OpenAI �̏ꍇ�A"gpt-4o-mini", "gpt-4o", "o1-mini", "o3-mini" �̒�����
            // �h���b�v�_�E���̏����ōŏ��̍��� "gpt-4o-mini" ���I�������z��
            Assert.IsNotNull(result, "���ʂ� null �łȂ�����");
            Assert.AreEqual("gpt-4o-mini", result.ToString(), "OpenAI�̏ꍇ�A�ŏ��̃��f�����I������邱��");
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
            // AzureOpenAI �̏ꍇ�A"gpt-4o-mini", "gpt-4o", "o1-mini" �̒�����
            // �h���b�v�_�E���̏����ōŏ��̍��� "gpt-4o-mini" ���I�������z��
            Assert.IsNotNull(result, "���ʂ� null �łȂ�����");
            Assert.AreEqual("gpt-4o-mini", result.ToString(), "AzureOpenAI�̏ꍇ�A�ŏ��̃��f�����I������邱��");
        }
    }
}
