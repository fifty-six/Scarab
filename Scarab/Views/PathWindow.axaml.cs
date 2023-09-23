using Projektanker.Icons.Avalonia;

namespace Scarab.Views;

public partial class PathWindow : ReactiveWindow<PathViewModel>
{
    public PathWindow()
    {
        InitializeComponent();

        this.WhenActivatedVM(Act);
    }

    private void Act(PathViewModel vm, CompositeDisposable d)
    {
        // TODO: i18n
        void OnNext(string? s)
        {
            switch (vm.Result)
            {
                case RootNotFoundError:
                {
                    VerificationExpander.IsVisible = false;
                    
                    VerificationBlock.IsVisible = true;
                    VerificationBlock.Text = "Root not found!";
                    break;
                }

                case AssemblyNotFoundError e:
                {
                    VerificationBlock.IsVisible = false;

                    VerificationExpander.IsVisible = true;
                    VerificationExpander.Header = "Assembly not found!";

                    var files = e.MissingFiles.Select(x => (x, success: false))
                                 .Prepend((e.Root, success: true));

                    ShowFiles(files);

                    break;
                }
                
                case PathNotSelectedError:
                {
                    VerificationExpander.IsVisible = false;

                    VerificationBlock.IsVisible = true;
                    VerificationBlock.Text = "No path selected!";
                    break;
                }

                case SuffixNotFoundError se:
                {
                    VerificationBlock.IsVisible = false;
                    
                    VerificationExpander.IsVisible = true;
                    VerificationExpander.Header = "Couldn't find Managed folder!";

                    ShowFiles(se.AttemptedSuffixes.Select(x => (x, success: false)));

                    break;
                }

                case ValidPath:
                {
                    Close(dialogResult: vm.Selection);
                    
                    break;
                }
            }
        }

        vm.WhenAnyValue(x => x.Selection)
          .Subscribe(OnNext)
          .DisposeWith(d);
    }

    private void ShowFiles(IEnumerable<(string path, bool success)> files)
    {
        VerificationPanel.Children.Clear();
        
        foreach (var (ind, file) in files.Select((x, ind) => (ind, x)))
        {
            var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*") };

            var suffixText = new TextBlock { Text = file.path };

            var icon = new Icon { 
                Value = file.success
                    ? "fa-solid fa-check"
                    : "fa-solid fa-xmark",
            };

            suffixText.SetValue(Grid.RowProperty, ind);
            suffixText.SetValue(Grid.ColumnProperty, 1);

            icon.SetValue(Grid.RowProperty, ind);
            icon.SetValue(Grid.ColumnProperty, 0);

            g.Children.Add(suffixText);
            g.Children.Add(icon);

            VerificationPanel.Children.Add(g);
        }
    }
}