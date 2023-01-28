namespace Scarab.Util;

public record struct DownloadProgressArgs
{
    public int  BytesRead  { get; internal set; }
    public int? TotalBytes { get; internal init; }
    
    public bool Completed { get; internal set; }

    public double? PercentComplete => TotalBytes is { } len ? 100 * (BytesRead / (double) len) : null;
}