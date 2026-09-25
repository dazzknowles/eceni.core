using System;

namespace Eceni.Core.Secrets.Exceptions
{
    public class VariableNotFoundException : Exception
    {
        public VariableNotFoundException(string variableName)
            : base($"The required variable '{variableName}' was not found.")
        {
            VariableName = variableName;
        }

        public string VariableName { get; }
    }
}
