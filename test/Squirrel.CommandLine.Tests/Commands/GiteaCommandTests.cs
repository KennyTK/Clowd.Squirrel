using System.CommandLine.Parsing;
using Squirrel.CommandLine.Commands;
using Xunit;

namespace Squirrel.CommandLine.Tests.Commands
{
    public abstract class GiteaCommandTests<T> : BaseCommandTests<T>
        where T : GiteaBaseCommand, new()
    {
        [Fact]
        public void BaseUrl_WithUrl_ParsesValue()
        {
            GiteaBaseCommand command = new T();

            ParseResult parseResult = command.ParseAndApply($"--baseUrl \"https://xkcd.com\"");

            Assert.Empty(parseResult.Errors);
            Assert.Equal("https://xkcd.com/", command.RepoUrl);
        }

        [Fact]
        public void BaseUrl_WithNonHttpValue_ShowsError()
        {
            GiteaBaseCommand command = new T();

            ParseResult parseResult = command.ParseAndApply($"--baseUrl \"file://minecraft.net\"");

            Assert.Equal(1, parseResult.Errors.Count);
            //Assert.Equal(command.RepoUrl, parseResult.Errors[0].SymbolResult?.Symbol);
            Assert.StartsWith("--baseUrl must contain a Uri with one of the following schems: http, https.", parseResult.Errors[0].Message);
        }

        [Fact]
        public void BaseUrl_WithRelativeUrl_ShowsError()
        {
            GiteaBaseCommand command = new T();

            ParseResult parseResult = command.ParseAndApply($"--baseUrl \"www.microsoft.com\"");

            Assert.Equal(1, parseResult.Errors.Count);
            //Assert.Equal(command.RepoUrl, parseResult.Errors[0].SymbolResult?.Symbol);
            Assert.StartsWith("--baseUrl must contain an absolute Uri.", parseResult.Errors[0].Message);
        }

        [Fact]
        public void Token_WithValue_ParsesValue()
        {
            GiteaBaseCommand command = new T();

            string cli = GetRequiredDefaultOptions() + $"--token \"abc\"";
            ParseResult parseResult = command.ParseAndApply(cli);

            Assert.Equal("abc", command.Token);
        }

        protected override string GetRequiredDefaultOptions()
        {
            return $"--repoUrl \"https://clowd.squirrel.com\" ";
        }
    }

    public class GiteaDownloadCommandTests : GiteaCommandTests<GiteaDownloadCommand>
    {
        [Fact]
        public void Pre_BareOption_SetsFlag()
        {
            var command = new GiteaDownloadCommand();

            string cli = GetRequiredDefaultOptions() + "--pre";
            ParseResult parseResult = command.ParseAndApply(cli);

            Assert.True(command.Pre);
        }
    }

    public class GiteaUploadCommandTests : GiteaCommandTests<GiteaUploadCommand>
    {
        public override bool ShouldBeNonEmptyReleaseDir => true;

        [Fact]
        public void Publish_BareOption_SetsFlag()
        {
            var command = new GiteaUploadCommand();

            string cli = GetRequiredDefaultOptions() + "--publish";
            ParseResult parseResult = command.ParseAndApply(cli);

            Assert.True(command.Publish);
        }

        [Fact]
        public void ReleaseName_WithName_ParsesValue()
        {
            var command = new GiteaUploadCommand();

            string cli = GetRequiredDefaultOptions() + $"--releaseName \"my release\"";
            ParseResult parseResult = command.ParseAndApply(cli);

            Assert.Equal("my release", command.ReleaseName);
        }
    }
}
