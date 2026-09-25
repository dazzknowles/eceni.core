namespace Eceni.Core.Secrets.Abstract
{
    /// <summary>
    /// Retrieves non-secret values from the configured variables source.
    /// Prefer IOptions&lt;T&gt; for structured application configuration.
    /// </summary>
    public interface IEceniVariables
    {
        string GetVariable(string name);
        string GetRequiredVariable(string name);
        T GetVariable<T>(string name);
    }
}
