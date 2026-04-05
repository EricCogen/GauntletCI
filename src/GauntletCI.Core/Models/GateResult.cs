namespace GauntletCI.Core.Models;

public sealed record GateResult(string GateName, bool Passed, string Summary, string? Output = null)
{
    public static GateResult Pass(string gateName, string summary) => new(gateName, true, summary);

    public static GateResult Fail(string gateName, string summary, string? output = null) => new(gateName, false, summary, output);
}
