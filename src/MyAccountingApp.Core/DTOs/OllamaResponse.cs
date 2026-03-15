using System.Text.RegularExpressions;

namespace MyAccountingApp.Core.DTOs;
public class OllamaResponse
{
    public string Response { get; init; } = string.Empty;

    public string GetJsonFromResponse()
    {
        Match match = Regex.Match(this.Response, @"\[[\s\S]*\]");

        if (match.Success)
        {
            return match.Value;
        }

        return string.Empty;
    }
}
