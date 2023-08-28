namespace Scarab.Models;

[Flags]
public enum Tag
{
    Boss = 1,
    Cosmetic = 1 << 1,
    Expansion = 1 << 2,
    Gameplay = 1 << 3,
    Library = 1 << 4,
    Utility = 1 << 5,
    
    All = 0
}