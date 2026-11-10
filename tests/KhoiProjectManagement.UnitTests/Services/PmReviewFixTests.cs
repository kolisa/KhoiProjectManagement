using FluentValidation;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Follow-ups from the code review of rich descriptions / effort.
    public class PmReviewFixTests
    {
        [Fact]
        public void ADescriptionThatOnlyOverflowsAfterSanitising_IsRefusedNotStored()
        {
            // Every link gains target + rel, so a description just under the limit can grow past it.
            var link = "<a href=\"https://example.com\">x</a>";
            var html = string.Concat(Enumerable.Repeat(link, 20000 / link.Length));
            Assert.True(html.Length <= RichText.MaxLength);

            Assert.Throws<InvalidOperationException>(() => RichText.SanitizeForStorage(html));
        }

        [Fact]
        public void ANormalDescription_IsStored()
        {
            Assert.Equal("<p>fine</p>", RichText.SanitizeForStorage("<p>fine</p>"));
        }

        [Fact]
        public async Task SanitisingFromManyThreadsAtOnce_StaysCorrect()
        {
            var tasks = Enumerable.Range(0, 64).Select(i => Task.Run(() => RichText.Sanitize($"<p>n{i}</p><script>x</script>")));
            var results = await Task.WhenAll(tasks);
            for (var i = 0; i < results.Length; i++) Assert.Equal($"<p>n{i}</p>", results[i]);
        }

        [Theory]
        [InlineData("strong", true)]
        [InlineData("HREF", true)]
        [InlineData("login", false)]
        [InlineData("strongly", false)]
        public void WordsThatOnlyExistInMarkup_AreRecognised(string term, bool markup) => Assert.Equal(markup, RichText.IsMarkupWord(term));

        [Fact]
        public void SearchingForAMarkupWord_DoesNotMatchEveryFormattedDescription()
        {
            var items = new List<ProjectTask>
            {
                new() { Id = 1, Title = "Login", Description = "<p><strong>bold</strong></p>" },
                new() { Id = 2, Title = "Strong passwords", Description = "<p>x</p>" },
            }.AsQueryable();

            var hits = PmWorkItemService.ApplyFilter(items, new WorkItemFilterDto { Search = "strong" }).Select(t => t.Id).ToList();

            Assert.Equal(new[] { 2 }, hits);     // by title only; the <strong> tag in item 1 is not a match
        }

        [Fact]
        public void TheEffortRequest_IsValidatedBeforeTheServiceSeesIt()
        {
            var validator = new SaveWorkItemEffortDtoValidator();
            var bad = new SaveWorkItemEffortDto { People = { new() { UserId = 1, Cells = { new() { Category = "Coffee", Points = 1 }, new() { Category = "DevEffort", Points = 5000 } } } } };
            var good = new SaveWorkItemEffortDto { People = { new() { UserId = 1, Cells = { new() { Category = "DevEffort", Points = 3 }, new() { Category = "Training", Points = null } } } } };

            Assert.False(validator.Validate(bad).IsValid);
            Assert.True(validator.Validate(good).IsValid);
        }
    }
}
