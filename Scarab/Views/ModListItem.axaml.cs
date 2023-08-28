using Avalonia.Controls.Metadata;

namespace Scarab.Views;

[PseudoClasses(":installed", ":installing", ":enabled", ":disabled", ":updated")]
public partial class ModListItem : ReactiveUserControl<ModItem>
{
    public ModListItem()
    {
        InitializeComponent();

        this.WhenActivatedVM((vm, d) =>
        {
            vm.WhenAnyValue(x => x.State)
              .Subscribe(OnStateChange)
              .DisposeWith(d);
        });
    }

    private void OnStateChange(ModState state)
    {
        switch (state)
        {
            case InstalledState { Enabled: var enabled, Updated: var updated }:
                PseudoClasses.Set(":installed", true);
                PseudoClasses.Set(":enabled", enabled);
                PseudoClasses.Set(":updated", updated);
                break;
            case NotInstalledState { Installing: var installing }:
                PseudoClasses.Set(":installed", false);
                PseudoClasses.Set(":enabled", false);
                PseudoClasses.Set(":updated", false);

                PseudoClasses.Set(":installing", installing);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
    }
}