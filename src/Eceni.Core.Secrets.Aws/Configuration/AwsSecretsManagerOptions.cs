namespace Eceni.Core.Secrets.Aws.Configuration
{
    public class AwsSecretsManagerOptions
    {
        public const string SectionName = "Eceni:Secrets:AwsSecretsManager";

        /// <summary>
        /// Optional AWS region system name, for example eu-west-2. When omitted, the AWS SDK resolves it normally.
        /// </summary>
        public string Region { get; set; }
    }
}
