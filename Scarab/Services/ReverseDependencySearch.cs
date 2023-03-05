using System;
using System.Collections.Generic;
using System.Linq;
using Scarab.Models;

namespace Scarab.Services;

public class ReverseDependencySearch
{
    //a dictionary to allow constant lookup times of ModItems from name
    private readonly Dictionary<string, ModItem> _allModItemsDictionary;

    public ReverseDependencySearch(IEnumerable<ModItem> allModItems)
    {
        _allModItemsDictionary = allModItems.ToDictionary(x => x.Name, x => x);
    }

    public IEnumerable<ModItem> GetAllEnabledDependents(ModItem item)
    {
        var dependants = new List<ModItem>();
        
        //check all enabled mods if they have a dependency on this mod
        foreach (var mod in _allModItemsDictionary.Values.Where(x => x.EnabledIsChecked))
        {
            if (IsDependent(mod, item))
            {
                dependants.Add(mod);
            }
        }
        return dependants;
    }

    private bool IsDependent(ModItem mod, ModItem targetMod)
    {
        foreach (var dependency in mod.Dependencies.Select(x => _allModItemsDictionary[x]))
        {
            //if the mod's listed dependency is the targetMod, it is a dependency
            if (dependency == targetMod) return true;

            //it also is a dependent if it has a transitive dependent
            if (IsDependent(dependency, targetMod)) return true;
        }

        return false;
    }
}