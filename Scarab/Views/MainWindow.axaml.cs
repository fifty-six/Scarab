namespace Scarab.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivatedVM((vm, d) =>
        {
            vm.WhenAnyValue(x => x.Content).Subscribe(v =>
              {
                  if (v is not null)
                      ModListTab.Content = v;
              })
              .DisposeWith(d);
        });
    }
}