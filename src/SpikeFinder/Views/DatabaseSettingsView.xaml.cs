#nullable enable
using ReactiveUI;
using SpikeFinder.Extensions;
using System;

namespace SpikeFinder.Views
{
    /// <summary>
    /// Interaction logic for DatabaseSettingsView.xaml
    /// </summary>
    public partial class DatabaseSettingsView
    {
        public DatabaseSettingsView()
        {
            InitializeComponent();

            IDisposable? whenActivated = null;

            whenActivated = this.WhenActivated(d =>
            {
                DbPathTextBox.Focus();

                d(whenActivated!);
            });
        }
    }
}
