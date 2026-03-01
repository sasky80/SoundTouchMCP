using Microsoft.Extensions.Options;

namespace SoundTouchMCP.Models;

public class SoundTouchConfigurationValidator : IValidateOptions<SoundTouchConfiguration>
{
    public ValidateOptionsResult Validate(string? name, SoundTouchConfiguration options)
    {
        var errors = new List<string>();

        if (options is null)
            return ValidateOptionsResult.Fail("SoundTouch configuration is missing.");

        if (options.Discovery is null)
            errors.Add("SoundTouch:Discovery section is required.");

        if (options.Discovery is not null)
        {
            if (options.Discovery.ProbeTimeoutMs is < 500 or > 30000)
                errors.Add("SoundTouch:Discovery:ProbeTimeoutMs must be between 500 and 30000.");

            if (options.Discovery.Zeroconf is null)
            {
                errors.Add("SoundTouch:Discovery:Zeroconf section is required.");
            }
            else
            {
                if (options.Discovery.Zeroconf.ScanTimeMs is < 500 or > 60000)
                    errors.Add("SoundTouch:Discovery:Zeroconf:ScanTimeMs must be between 500 and 60000.");

                if (options.Discovery.Zeroconf.SocketRetries is < 1 or > 10)
                    errors.Add("SoundTouch:Discovery:Zeroconf:SocketRetries must be between 1 and 10.");

                if (options.Discovery.Zeroconf.SocketRetryDelayMs is < 100 or > 10000)
                    errors.Add("SoundTouch:Discovery:Zeroconf:SocketRetryDelayMs must be between 100 and 10000.");

                if (options.Discovery.Zeroconf.DiscoveryPasses is < 1 or > 10)
                    errors.Add("SoundTouch:Discovery:Zeroconf:DiscoveryPasses must be between 1 and 10.");

                if (options.Discovery.Zeroconf.PassDelayMs is < 0 or > 10000)
                    errors.Add("SoundTouch:Discovery:Zeroconf:PassDelayMs must be between 0 and 10000.");
            }
        }

        if (errors.Count > 0)
            return ValidateOptionsResult.Fail(errors);

        return ValidateOptionsResult.Success;
    }
}
