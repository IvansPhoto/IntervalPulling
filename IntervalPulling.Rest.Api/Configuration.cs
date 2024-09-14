using System.ComponentModel.DataAnnotations;

namespace IntervalPulling.Rest.Api;

internal sealed class Configuration
{
    [Range(1, 100)]
    public int ProcessorNumber { get; set; }
}