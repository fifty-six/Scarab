using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive;
using System.Text;

namespace Modinstaller2.Models
{
    public class ModItem : INotifyPropertyChanged
    {
        internal bool? _enabled;
        internal bool _installed;

        public string Name { get; set; }

        public bool? Enabled
        {
            get => _enabled;

            set
            {
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        public bool Installed
        {
            get => _installed;

            set
            {
                _installed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnEnable()
        {
            System.Diagnostics.Debug.WriteLine($"Enabled: {Enabled}, Installed: {Installed}");
        }

        public void OnInstall() 
        {
            if (!Installed)
            {
                Enabled = null;
            }
            else
            {
                OnEnable();
                Enabled = true;
            }
        }
    }
}