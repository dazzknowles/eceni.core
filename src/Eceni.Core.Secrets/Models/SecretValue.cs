namespace Eceni.Core.Secrets.Models
{
    /// <summary>
    /// A value returned by a secret provider. ToString deliberately redacts the value.
    /// </summary>
    public sealed class SecretValue
    {
        public SecretValue(string value, string version = null)
        {
            Value = value;
            Version = version;
        }

        public string Value { get; }
        public string Version { get; }

        public override string ToString()
        {
            return "[REDACTED]";
        }
    }
}
