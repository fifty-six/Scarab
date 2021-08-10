using System.Threading.Tasks;
using Modinstaller2.Models;

namespace Modinstaller2.Interfaces
{
    public interface IModSource
    {
        ModState FromManifest(Manifest manifest);

        Task RecordInstall(ModItem item);

        Task RecordUninstall(ModItem item);
    }
}