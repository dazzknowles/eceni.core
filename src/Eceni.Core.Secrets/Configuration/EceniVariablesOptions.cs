namespace Eceni.Core.Secrets.Configuration
{
    public class EceniVariablesOptions
    {
        public const string SectionName = "Eceni:Variables";

        public string Provider { get; set; } = "Configuration";
        public string Section { get; set; } = "Variables";
    }
}
