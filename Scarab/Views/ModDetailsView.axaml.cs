using System.Diagnostics;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using HttpRequestException = System.Net.Http.HttpRequestException;

namespace Scarab.Views;

public partial class ModDetailsView : ReactiveUserControl<ModPageViewModel>
{
    private readonly ReactiveCommand<Unit, Unit> _fetch;
    private WindowNotificationManager? _notify;

    public ModDetailsView()
    {
        _fetch = ReactiveCommand.CreateFromTask(FetchSelected);

        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var tl = TopLevel.GetTopLevel(this);

        _notify = new WindowNotificationManager(tl) { MaxItems = 3 };
    }

    protected override void OnInitialized()
    {
        // Insert it early so we override the default SvgPlugin
        MdReadme.Plugins.Plugins.Insert(0, new SvgPlugin());

        ReadmeTab
            .WhenAnyValue(x => x.IsSelected)
            .Subscribe(
                selected =>
                {
                    if (!selected)
                        return;

                    _fetch.Execute().Subscribe();
                }
            );

        ViewModel.WhenAnyValue(x => x.SelectedModItem)
                 .Subscribe(
                     mod =>
                     {
                         // Clear the display regardless of whether or not it's selected
                         // or we have a mod, as it's preferable to have an empty screen
                         // than a wrong one
                         MdReadme.Markdown = null;
                         MdReadme.AssetPathRoot = null;

                         // If we've swapped to something where it isn't selected,
                         // or unselected a mod - then we're done
                         if (mod is null || !ReadmeTab.IsSelected)
                         {
                             return;
                         }

                         // Otherwise, we need to re-fetch the content to have it properly match
                         _fetch.Execute().Subscribe();
                     }
                 );
    }

    private async Task FetchSelected()
    {
        if (ViewModel?.SelectedModItem is not { } item)
            return;

        if (!item.Repository.StartsWith("https://github.com"))
            return;

        try
        {
            var res = await ViewModel.FetchReadme(item);
            
            if (res is var (repo, content)) {
                MdReadme.Markdown = content;
                MdReadme.AssetPathRoot = repo;
            }
            else
            {
                MdReadme.Markdown = "No README found!";
                MdReadme.AssetPathRoot = null;
            }
        }
        catch (HttpRequestException e)
        {
            Log.Error(e, "Unable to fetch README from repo {Uri}! {Msg}", item.Repository, e.Message);

            _notify?.Show(
                new Notification(
                    "Failed to get README!",
                    $"Returned status code {e.StatusCode}",
                    NotificationType.Error
                )
            );
        }
    }


    [UsedImplicitly]
    private void RepositoryTextClick(object? sender, PointerReleasedEventArgs _)
    {
        if (sender is not TextBlock txt)
        {
            Log.Warning(
                $"{nameof(RepositoryTextClick)} called with non TextBlock sender {{SenderType}}!",
                sender?.GetType().Name ?? "null"
            );
            return;
        }

        if (string.IsNullOrEmpty(txt.Text))
            return;

        try
        {
            Process.Start
            (
                new ProcessStartInfo(txt.Text)
                {
                    UseShellExecute = true
                }
            );
        }
        catch (Exception e)
        {
            Log.Error(e, $"{nameof(RepositoryTextClick)} process spawn failed!`");
        }
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Log.Debug("sssssssssssssss");
    }
}