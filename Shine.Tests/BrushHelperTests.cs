using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Media;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class BrushHelperTests
    {
        [TestMethod]
        public void ConvertBrushToHex_ReturnsCorrectHex_ForSolidColorBrush()
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)); // Red
            string hex = BrushHelper.ConvertBrushToHex(brush);
            Assert.AreEqual("#FF0000", hex);
        }

        [TestMethod]
        public void ConvertBrushToHex_ReturnsBlack_ForNonSolidColorBrush()
        {
            // SolidColorBrush à»äOÇÃèÍçáÅA"#000000" Ç™ï‘ÇÈ
            System.Windows.Media.Brush nonSolidBrush = new DrawingBrush();
            string hex = BrushHelper.ConvertBrushToHex(nonSolidBrush);
            Assert.AreEqual("#000000", hex);
        }
    }
}
