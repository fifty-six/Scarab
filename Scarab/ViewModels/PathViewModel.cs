namespace Scarab.ViewModels;

[UsedImplicitly]
public partial class PathViewModel : ViewModelBase
{
    public string? Selection => Result.Path;

    [Notify]
    private PathResult _result;
    
    public ReactiveCommand<Unit, Unit> ChangePath { get; }
    
    public PathViewModel(PathResult res)
    {
        ChangePath = ReactiveCommand.CreateFromTask(ChangePathAsync);
        Log.Debug("Result = {Result}", res);
        Result = _result = res;
    }

    private async Task ChangePathAsync()
    {
        Result = await PathUtil.TrySelection();
        
        Log.Information("Set selection to new path: {Path}", Selection);
    }
}