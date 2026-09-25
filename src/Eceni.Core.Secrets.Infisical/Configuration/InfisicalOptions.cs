namespace Eceni.Core.Secrets.Infisical.Configuration
{
    public class InfisicalOptions
    {
        public const string SectionName = "Eceni:Secrets:Infisical";

        public string Host { get; set; } = "https://app.infisical.com";
        public string ProjectId { get; set; }
        public string Environment { get; set; }
        public string SecretPath { get; set; } = "/";
        public bool ExpandSecretReferences { get; set; } = true;
        public string ClientIdEnvironmentVariable { get; set; } = "INFISICAL_CLIENT_ID";
        public string ClientSecretEnvironmentVariable { get; set; } = "INFISICAL_CLIENT_SECRET";
    }
}
