using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Shine
{
    /// <summary>
    /// API キーをマスク／アンマスクできるプロパティ エディタ
    /// ・ドロップダウン開いた直後は「******************」を表示
    /// ・「表示」チェックで本物のキーを表示
    /// </summary>
    public class ApiKeyEditor : UITypeEditor
    {
        // グリッド上でもエディタ内でも使う固定マスク文字列
        private const string _mask = "******************";

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            => UITypeEditorEditStyle.DropDown;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (provider?.GetService(typeof(IWindowsFormsEditorService))
                is not IWindowsFormsEditorService svc)
                return value;

            // オリジナルのキーを覚えておく
            string original = value as string ?? string.Empty;

            // ドロップダウン用パネル
            var panel = new Panel
            {
                Width = 500,
                Height = 60
            };

            // テキストボックス（最初は固定マスクを表示）
            var tb = new TextBox
            {
                Parent = panel,
                Text = _mask,
                UseSystemPasswordChar = false, // 星を文字列として見せる
                Width = 500,
                Left = 0,
                Top = 0
            };

            // マスク解除用チェックボックス
            var chk = new CheckBox
            {
                Parent = panel,
                Text = "表示",
                AutoSize = true,
                Left = 0,
                Top = tb.Bottom + 6,
                Checked = false
            };

            chk.CheckedChanged += (_, __) =>
            {
                if (chk.Checked)
                {
                    // チェックON → 本物のキーを表示
                    tb.Text = original;
                    tb.UseSystemPasswordChar = false;
                }
                else
                {
                    // チェックOFF → 再び固定マスク表示
                    tb.Text = _mask;
                    tb.UseSystemPasswordChar = false;
                }
            };

            // ドロップダウンを表示
            svc.DropDownControl(panel);

            // ドロップダウンを閉じた後、返す値を決定
            // ・ユーザーが何も触らず閉じた（tb.Text == Mask）→ 元のキーを返す
            // ・tb.Text != Mask → ユーザーが新しいキーを入力した可能性 → その文字列を返す
            if (tb.Text == _mask)
                return original;
            else
                return tb.Text;
        }
    }
}
