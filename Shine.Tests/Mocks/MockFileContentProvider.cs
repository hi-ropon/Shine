namespace Shine.Tests.Mocks
{
    public class MockFileContentProvider : FileContentProvider
    {
        private readonly Dictionary<string, string> _fileContents = new Dictionary<string, string>();

        public void SetFileContent(string fileName, string content)
        {
            _fileContents[fileName] = content;
        }

        public override string GetFileContent(string fileName)
        {
            return _fileContents.TryGetValue(fileName, out string content) ? content : string.Empty;
        }
    }
}
