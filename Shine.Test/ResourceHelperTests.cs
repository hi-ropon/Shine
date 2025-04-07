using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class ResourceHelperTests
    {
        [TestMethod]
        public void LoadResourceAsBase64_ReturnsNull_ForInvalidResource()
        {
            // 存在しないリソースパスの場合、null が返る
            string result = ResourceHelper.LoadResourceAsBase64("Invalid.Resource.Path", typeof(ResourceHelper));
            Assert.IsNull(result);
        }
    }
}
