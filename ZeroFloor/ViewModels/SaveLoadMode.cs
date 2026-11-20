namespace ZeroFloor.ViewModels;

/// <summary>Один и тот же экран (SaveLoadView) показывает либо слоты для записи (с
/// подтверждением перезаписи), либо слоты для загрузки (включая автосохранение) — различие
/// только в том, что происходит по клику на слот (см. SaveLoadViewModel.OnSlotSelected).</summary>
public enum SaveLoadMode
{
    Save,
    Load
}
