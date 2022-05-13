using Scarab.ViewModels;

namespace Scarab.Mock;

public static class MockViewModel
{
    public static ModListViewModel DesignInstance => new(null!, new MockDatabase(), null!, null!);
}