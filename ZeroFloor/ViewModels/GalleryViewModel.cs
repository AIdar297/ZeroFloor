using System.Linq;
using System.Windows.Input;
using ZeroFloor.Common;

namespace ZeroFloor.ViewModels;

public sealed class GalleryViewModel : ViewModelBase
{
    public GalleryViewModel(Action onBack)
    {
        BackCommand = new RelayCommand(_ => onBack());

        // Реальных иллюстраций ещё нет — все плитки честно показаны как заблокированные,
        // а не подменены случайными изображениями (см. CLAUDE.md, правило 6).
        Tiles = Enumerable.Range(1, 12)
            .Select(index => new GalleryTileViewModel { Index = index, IsUnlocked = false })
            .ToList();
    }

    public IReadOnlyList<GalleryTileViewModel> Tiles { get; }
    public ICommand BackCommand { get; }
}
