using System;
using System.Collections.Generic;

namespace Eceni.Core.Secrets.Configuration
{
    public class EceniSecretsOptions
    {
        public const string SectionName = "Eceni:Secrets";

        public string Provider { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan MissingCacheDuration { get; set; } = TimeSpan.FromSeconds(30);
        public bool ValidateOnStart { get; set; }
        public List<string> Required { get; set; } = new();
        public Dictionary<string, string> Mappings { get; set; } = new();
    }
}
