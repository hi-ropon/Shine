// �t�@�C����: ResourceHelperTests.cs
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
            // �����\�[�X�p�X�͎��ۂɃv���W�F�N�g�ɒǉ��������ߍ��݃��\�[�X�̖��O��Ԃƈ�v�����邱��
            string resourcePath = "Shine.Resources.icon.png";

            // Act
            string base64 = ResourceHelper.LoadResourceAsBase64(resourcePath, typeof(ResourceHelperTests));

            // Assert
            Assert.IsNotNull(base64, "���ߍ��݃��\�[�X���������ǂݍ��܂�Base64�ϊ�����Ă��邱��");
        }
    }
}
