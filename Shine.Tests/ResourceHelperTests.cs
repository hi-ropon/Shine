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
            // ���݂��Ȃ����\�[�X�p�X�̏ꍇ�Anull ���Ԃ�
            string result = ResourceHelper.LoadResourceAsBase64("Invalid.Resource.Path", typeof(ResourceHelper));
            Assert.IsNull(result);
        }
    }
}
