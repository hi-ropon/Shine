using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
namespace Shine
{
    /// <summary>
    /// モデル名を選択するためのエディタ
    /// </summary>
    public class ModelNameEditor : UITypeEditor
    {
        /// <summary>
        /// エディタのスタイルを指定する
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        /// <summary>
        /// ドロップダウンリストを表示する
        /// </summary>
        /// <param name="context"></param>
        /// <param name="provider"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context?.Instance is AiAssistantOptions options && provider != null)
            {
                IWindowsFormsEditorService editorService =
                    provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
                if (editorService != null)
                {
                    ListBox listBox = new ListBox
                    {
                        SelectionMode = SelectionMode.One
                    };
                    if (options.Provider == OpenAiProvider.OpenAI)
                    {
                        // OpenAI のモデルリスト
                        listBox.Items.Add("gpt-4o-mini");
                        listBox.Items.Add("gpt-4o");
                        listBox.Items.Add("o1-mini");
                        listBox.Items.Add("o3-mini");
                    }
                    else if (options.Provider == OpenAiProvider.AzureOpenAI)
                    {
                        // Azure OpenAI のモデルリスト
                        listBox.Items.Add("gpt-4o-mini");
                        listBox.Items.Add("gpt-4o");
                        listBox.Items.Add("o1-mini");
                    }
                    listBox.SelectedItem = value;
                    listBox.SelectedIndexChanged += (s, e) =>
                    {
                        editorService.CloseDropDown();
                    };
                    editorService.DropDownControl(listBox);
                    if (listBox.SelectedItem != null)
                    {
                        value = listBox.SelectedItem.ToString();
                    }
                }
            }
            return value;
        }
    }
}