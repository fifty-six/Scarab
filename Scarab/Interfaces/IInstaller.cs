using System;
using System.Threading.Tasks;
using Scarab.Models;

namespace Scarab.Interfaces
{
    public interface IInstaller
    {
        public void Toggle(ModItem mod);

        public Task Install(ModItem mod, Action<ModProgressArgs> setProgress, bool enable);

        public Task Uninstall(ModItem mod);

        public Task InstallApi();

        public Task ToggleApi();
        public bool HasVanilla { get; }
        public Task<bool> CheckAPI();
    }
}