using GauntletCI.Core.Models;

namespace GauntletCI.Core.Telemetry;

public sealed class TelemetryEmitter
{
    public Task EmitAsync(EvaluationResult result, GauntletConfig config, CancellationToken cancellationToken)
    {
        if (!config.Telemetry)
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
