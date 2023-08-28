namespace Scarab.Models;

public record struct ModProgressArgs
{
    public DownloadProgressArgs? Download  { get; internal init; }
    public bool                  Completed { get; internal init; }
}