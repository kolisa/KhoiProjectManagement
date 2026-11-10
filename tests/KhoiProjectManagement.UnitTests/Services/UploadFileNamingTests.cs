using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // These must pass identically on Windows and on the Linux server: Path.GetFileName alone only understands the
    // current OS's separators, which left Windows-style client paths intact on Linux.
    public class UploadFileNamingTests
    {
        [Theory]
        [InlineData("shot.png", "shot.png")]
        [InlineData(@"..\..\evil\shot.PNG", "shot.PNG")]
        [InlineData("../../evil/shot.PNG", "shot.PNG")]
        [InlineData(@"C:\Users\me\Desktop\report.pdf", "report.pdf")]
        [InlineData("/etc/passwd", "passwd")]
        [InlineData(@"mixed/..\path\file.txt", "file.txt")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData("folder/", "")]
        [InlineData(@"folder\", "")]
        public void SanitizeFileName_RemovesAnyDirectoryPart_OnEveryOs(string? input, string expected) =>
            Assert.Equal(expected, UploadFileNaming.SanitizeFileName(input));

        [Theory]
        [InlineData(@"..\..\evil\shot.PNG")]
        [InlineData("../../evil/shot.PNG")]
        public void BuildStoredFileName_NeverContainsASeparatorOrADotDotSegment(string input)
        {
            var stored = UploadFileNaming.BuildStoredFileName(input);

            Assert.DoesNotContain("/", stored);
            Assert.DoesNotContain("\\", stored);
            Assert.EndsWith("_shot.PNG", stored);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("dir/")]
        public void BuildStoredFileName_FallsBackToAPlaceholderNameWhenNothingIsUsable(string? input) =>
            Assert.EndsWith("_file", UploadFileNaming.BuildStoredFileName(input));

        [Fact]
        public void BuildStoredFileName_IsUniquePerCall() =>
            Assert.NotEqual(UploadFileNaming.BuildStoredFileName("a.png"), UploadFileNaming.BuildStoredFileName("a.png"));
    }
}
