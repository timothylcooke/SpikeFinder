#nullable enable

using ReactiveUI;
using System;

namespace SpikeFinder.Views
{
    /// <summary>
    /// Interaction logic for SpikesView.xaml
    /// </summary>
    public partial class SpikesView
    {
        public SpikesView()
        {
            InitializeComponent();

            IDisposable? whenActivated = null;
            whenActivated = this.WhenActivated(d =>
            {
                d(this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext));

                d(whenActivated!);
            });
        }
    }
}
