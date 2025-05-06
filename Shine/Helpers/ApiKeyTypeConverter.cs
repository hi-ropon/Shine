// ファイル名: ApiKeyTypeConverter.cs
using System;
using System.ComponentModel;
using System.Globalization;

namespace Shine
{
    public class ApiKeyTypeConverter : StringConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                // グリッド上では必ず固定マスク文字列を返す
                return "******************";
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
