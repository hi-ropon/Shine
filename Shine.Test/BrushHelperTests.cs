// ファイル名: BrushHelperTests.cs
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class BrushHelperTests
    {
        [TestMethod]
        public void ConvertBrushToHex_ReturnsCorrectHex_ForSolidColorBrush()
        {
            // Arrange: 赤色 (RGB: 255, 0, 0)
            SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));

            // Act
            string hex = BrushHelper.ConvertBrushToHex(brush);

            // Assert
            Assert.AreEqual("#FF0000", hex, "赤色の変換が正しく行われていること");
        }
    }
}
