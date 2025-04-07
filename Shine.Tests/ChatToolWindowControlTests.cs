using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shine.Tests
{
    [TestClass]
    public class ChatToolWindowControlTests
    {
        private Mock<IChatClientService> _mockChatService;
        private Mock<FileContentProvider> _mockFileProvider;
        private Mock<Settings> _mockSettings;
        private ChatToolWindowControl _control;

        [TestInitialize]
        public void Setup()
        {
            // Initialize mocks
            _mockChatService = new Mock<IChatClientService>();
            _mockFileProvider = new Mock<FileContentProvider>();
            _mockSettings = new Mock<Settings>(It.IsAny<System.Windows.Controls.ComboBox>());

            // Setup default responses
            _mockChatService
                .Setup(x => x.GetChatResponseAsync(It.IsAny<string>()))
                .ReturnsAsync("Test response");

            _mockFileProvider
                .Setup(x => x.GetFileContent(It.IsAny<string>()))
                .Returns("Test file content");

            // Initialize control
            Application.Current = new System.Windows.Application();
            _control = new ChatToolWindowControl();
        }

        [TestMethod]
        public void Constructor_InitializesComponents()
        {
            Assert.IsNotNull(_control);
            Assert.IsNotNull(_control.InputRichTextBox);
            Assert.IsNotNull(_control.ChatHistoryWebView);
        }

        [TestMethod]
        public void ThemeColor_UpdatesControlColors()
        {
            // Arrange
            var expectedBrush = System.Windows.Media.Brushes.White;

            // Act
            _control.ThemeColor();

            // Assert
            Assert.AreEqual(expectedBrush, _control.TokenCountLabel.Foreground);
            Assert.AreEqual(expectedBrush, _control.TokenCountTextBox.Foreground);
        }

        [TestMethod]
        public async Task SendButton_Click_ProcessesUserInput()
        {
            // Arrange
            string userInput = "Test message";
            _mockChatService
                .Setup(x => x.GetChatResponseAsync(It.IsAny<string>()))
                .ReturnsAsync("Response to test message");

            // Act
            _control.SendButton_Click(null, null);

            // Assert
            _mockChatService.Verify(x => x.GetChatResponseAsync(It.Is<string>(
                s => s.Contains(userInput))), Times.Once);
        }

        [TestMethod]
        public void ProcessUserInputForFileContent_HandlesAtMentions()
        {
            // Arrange
            string input = "@testfile.cs some text";
            _mockFileProvider
                .Setup(x => x.GetFileContent("testfile.cs"))
                .Returns("file content");

            // Act
            string result = _control.ProcessUserInputForFileContent(input);

            // Assert
            Assert.IsTrue(result.Contains("testfile.cs"));
            Assert.IsTrue(result.Contains("file content"));
        }

        [TestMethod]
        public void UpdateTokenCount_CalculatesCorrectly()
        {
            // Arrange
            string input = "Test input";

            // Act
            _control.UpdateTokenCount(input);

            // Assert
            Assert.AreEqual("2", _control.TokenCountTextBox.Text); // Assuming "Test input" is 2 tokens
        }

        [TestCleanup]
        public void Cleanup()
        {
            System.Windows.Application.Current = null;
        }
    }
}
