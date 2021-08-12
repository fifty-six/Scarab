using System.Threading.Tasks;
using Scarab.Models;

namespace Scarab.Interfaces
{
    public interface IModSource
    {
        ModState FromManifest(Manifest manifest);

        Task RecordInstall(ModItem item);

        Task RecordUninstall(ModItem item);
    }
}