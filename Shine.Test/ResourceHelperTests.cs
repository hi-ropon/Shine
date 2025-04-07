// ファイル名: ResourceHelperTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class ResourceHelperTests
    {
        [TestMethod]
        public void LoadResourceAsBase64_ReturnsNonNull_ForExistingResource()
        {
            // Arrange
            // ※リソースパスは実際にプロジェクトに追加した埋め込みリソースの名前空間と一致させること
            string resourcePath = "Shine.Resources.icon.png";

            // Act
            string base64 = ResourceHelper.LoadResourceAsBase64(resourcePath, typeof(ResourceHelperTests));

            // Assert
            Assert.IsNotNull(base64, "埋め込みリソースが正しく読み込まれBase64変換されていること");
        }
    }
}
