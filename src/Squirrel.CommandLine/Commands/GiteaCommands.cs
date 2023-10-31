using System;

namespace Squirrel.CommandLine.Commands
{
    public class GiteaBaseCommand : BaseCommand
    {
        public string RepoUrl { get; private set; }

        public string Token { get; private set; }

        protected GiteaBaseCommand(string name, string description)
            : base(name, description)
        {
            AddOption<Uri>((v) => RepoUrl = v.ToAbsoluteOrNull(), "--repoUrl")
                .SetDescription("Url of the Gitea repository (eg. 'https://MyGiteaHost.com/RepoOwner/Repo').")
                .SetRequired()
                .MustBeValidHttpUri();

            AddOption<string>((v) => Token = v, "--token")
                .SetDescription("Token to use as login credentials. Must be input as \"token 9e876...\" or \"bearer 9e876...\"")
                .SetRequired();
        }
    }

    public class GiteaDownloadCommand : GiteaBaseCommand
    {
        public bool Pre { get; private set; }

        public GiteaDownloadCommand()
            : base("Gitea", "Download latest release from Gitea repository.")
        {
            AddOption<bool>((v) => Pre = v, "--pre")
                .SetDescription("Get latest pre-release instead of stable.");
        }
    }

    public class GiteaUploadCommand : GiteaBaseCommand
    {
        public bool Publish { get; private set; }

        public string ReleaseName { get; private set; }

        public GiteaUploadCommand()
            : base("Gitea", "Upload releases to a Gitea repository.")
        {
            AddOption<bool>((v) => Publish = v, "--publish")
                .SetDescription("Publish release instead of creating draft.");

            AddOption<string>((v) => ReleaseName = v, "--releaseName")
                .SetDescription("A custom name for created release.")
                .SetArgumentHelpName("NAME");

            ReleaseDirectoryOption.SetRequired();
            ReleaseDirectoryOption.MustNotBeEmpty();
        }
    }
}
