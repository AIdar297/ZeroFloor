using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Views.Controls;

public partial class LogPanelControl : UserControl
{
    public LogPanelControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is NovelViewModel oldViewModel)
        {
            ((INotifyCollectionChanged)oldViewModel.LogEntries).CollectionChanged -= OnLogEntriesChanged;
        }

        if (e.NewValue is NovelViewModel newViewModel)
        {
            ((INotifyCollectionChanged)newViewModel.LogEntries).CollectionChanged += OnLogEntriesChanged;
        }
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        LogScrollViewer.ScrollToEnd();
}
