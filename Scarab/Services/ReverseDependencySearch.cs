namespace Scarab.Services;

public class ReverseDependencySearch
{
    // a dictionary to allow constant lookup times of ModItems from name
    private readonly Dictionary<string, ModItem> _items;

    public ReverseDependencySearch(IEnumerable<ModItem> allModItems)
    {
        _items = allModItems.ToDictionary(x => x.Name, x => x);
    }

    public List<ModItem> GetAllEnabledDependents(ModItem item)
    {
        // Check all enabled mods if they have a dependency on this mod
        return _items.Values.Where(m => m.Enabled && IsDependent(m, item)).ToList();
    }

    public List<ModItem> GetDependents(ModItem item) => _items.Values.Where(m => m.Installed && IsDependent(m, item)).ToList();

    private bool IsDependent(ModItem mod, ModItem targetMod)
    {
        foreach (var dependency in mod.Dependencies.Select(x => _items[x]))
        {
            // If the mod's listed dependency is the targetMod, it's a dependency
            if (dependency == targetMod) 
                return true;

            // It's also a dependent if it has a transitive dependent
            if (IsDependent(dependency, targetMod)) 
                return true;
        }

        return false;
    }
}