using System;
using System.Threading.Tasks;
using Scarab.Models;

namespace Scarab.Interfaces
{
    public interface IInstaller
    {
        public void Toggle(ModItem mod);

        public Task Install(ModItem mod, Action<double> setProgress, bool enable);

        public Task Uninstall(ModItem mod);
    }
}