namespace KaiROS.AI.WinUI.Models;

/// <summary>
/// User-selectable context window size options.
/// Auto = system calculates the maximum safe size from model metadata + available RAM.
/// </summary>
public enum ContextWindowOption
{
    Auto = 0,       // MIN(model_max, ram_cap) — recommended
    Small = 2048,
    Default = 4096,
    Standard = 8192,
    Extended = 16384,
    Large = 32768
}

public static class ContextWindowOptionExtensions
{
    public static string ToDisplayString(this ContextWindowOption option) => option switch
    {
        ContextWindowOption.Auto     => "Auto (Maximum Safe)",
        ContextWindowOption.Small    => "Small (2K tokens)",
        ContextWindowOption.Default  => "Default (4K tokens)",
        ContextWindowOption.Standard => "Standard (8K tokens)",
        ContextWindowOption.Extended => "Extended (16K tokens)",
        ContextWindowOption.Large    => "Large (32K tokens)",
        _                            => option.ToString()
    };

    public static uint ToTokens(this ContextWindowOption option) => (uint)option;
}
