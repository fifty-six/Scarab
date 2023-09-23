namespace Scarab.Util;

public abstract record PathResult
{
    public string? Path => this switch
    {
        // Not a failure
        ValidPath v => System.IO.Path.Combine(v.Root, v.Suffix),

        RootNotFoundError => null,
        SuffixNotFoundError s => s.Root,
        AssemblyNotFoundError a => a.Root,
        PathNotSelectedError => null,
        
        _ => throw new ArgumentOutOfRangeException()
    };
}

public record ValidPath(string Root, string Suffix) : PathResult;

public record RootNotFoundError : PathResult;
public record SuffixNotFoundError(string Root, string[] AttemptedSuffixes) : PathResult;
public record AssemblyNotFoundError(string Root, string[] MissingFiles) : PathResult;
public record PathNotSelectedError : PathResult;