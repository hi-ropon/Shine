using System;
using System.ComponentModel;
using System.Globalization;

namespace Shine
{
    /// <summary>PropertyGrid で API キーを常に固定マスク表示にする TypeConverter</summary>
    public class ApiKeyTypeConverter : StringConverter
    {
        private const string _mask = "******************";

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture,
                                         object value, Type destinationType)
        {
            // 表示用は常に固定マスク
            if (destinationType == typeof(string)) return _mask;
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                // ユーザーが何も編集していない場合は Mask が飛んで来る。
                // そのときは現在値を保持し、Mask 以外なら新しいキーとして採用。
                if (s == _mask && context?.PropertyDescriptor != null && context.Instance != null)
                {
                    return context.PropertyDescriptor.GetValue(context.Instance);
                }
                return s.Trim();
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
