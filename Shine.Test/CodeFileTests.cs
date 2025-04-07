using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shine;

namespace Shine.Tests
{
    [TestClass]
    public class CodeFileTests
    {
        [DataTestMethod]
        [DataRow("test.cs", true)]
        [DataRow("test.vb", true)]
        [DataRow("test.cpp", true)]
        [DataRow("test.h", true)]
        [DataRow("test.csv", true)]
        [DataRow("test.xml", true)]
        [DataRow("test.json", true)]
        [DataRow("test.txt", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void IsCodeFile_ReturnsExpected(string fileName, bool expected)
        {
            bool result = CodeFile.IsCodeFile(fileName);
            Assert.AreEqual(expected, result);
        }
    }
}
