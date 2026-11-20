using ZeroFloor.Models;
using ZeroFloor.ViewModels;

namespace ZeroFloor.Tests;

/// <summary>«Сборка разрушенного кода» (Глава 8): подделки, распад записи, штраф и серии.</summary>
public sealed class CodeAssemblyViewModelTests
{
    private static CodeAssemblyLayout Layout(double decay = 0, double penalty = 0, double combo = 0, int maxErrors = 3) => new()
    {
        Pairs = new[]
        {
            new CodeFragmentPair { CleanText = "ДОПУСК АННУЛИРОВАН", CorruptedHint = "Д●ПУ●К АНН●Л●РОВАН" },
            new CodeFragmentPair { CleanText = "ПРЕЕМНИК ПОДТВЕРЖДЁН", CorruptedHint = "ПРЕ●МН●К ПОД●ВЕРЖДЁН" }
        },
        Decoys = new[] { "ДОПУСК ПОДТВЕРЖДЁН" },
        TimeLimitSeconds = 40,
        MaxErrors = maxErrors,
        DecaySeconds = decay,
        ErrorTimePenalty = penalty,
        ComboWindowSeconds = combo,
        ComboBonusSeconds = combo > 0 ? 3 : 0
    };

    [Fact]
    public void Decoy_IsInPool_AndCountsAsErrorWithTimePenalty()
    {
        var vm = new CodeAssemblyViewModel(Layout(penalty: 5), () => { }, () => { });
        Assert.Equal(3, vm.Fragments.Count);
        var decoy = vm.Fragments.Single(f => f.IsDecoy);

        vm.TryPlace(decoy, vm.Slots[0]);

        Assert.Equal(1, vm.ErrorCount);
        Assert.False(vm.Slots[0].IsFilled);
        Assert.Equal(35, vm.RemainingSeconds, 3);
        vm.Dispose();
    }

    [Fact]
    public void Decay_DimsLettersInOpenLines_OverTime()
    {
        var vm = new CodeAssemblyViewModel(Layout(decay: 1), () => { }, () => { });
        var before = vm.RecordIntegrity;
        vm.Simulate(5.05);
        Assert.True(vm.RecordIntegrity < before);
        vm.Dispose();
    }

    [Fact]
    public void QuickConsecutiveHits_GiveComboBonus_AndAllLinesWin()
    {
        var won = false;
        var vm = new CodeAssemblyViewModel(Layout(combo: 4), () => won = true, () => { });
        vm.TryPlace(vm.Fragments.First(f => f.PairIndex == 0), vm.Slots[0]);
        var afterFirst = vm.RemainingSeconds;
        vm.Simulate(1);
        vm.TryPlace(vm.Fragments.First(f => f.PairIndex == 1), vm.Slots[1]);

        Assert.True(won);
        Assert.Equal(1, vm.Combo);
        Assert.True(vm.RemainingSeconds > afterFirst);
        vm.Dispose();
    }

    [Fact]
    public void ErrorLimit_LeadsToFailure()
    {
        var failed = false;
        var vm = new CodeAssemblyViewModel(Layout(maxErrors: 2), () => { }, () => failed = true);
        var decoy = vm.Fragments.Single(f => f.IsDecoy);
        vm.TryPlace(decoy, vm.Slots[0]);
        vm.TryPlace(decoy, vm.Slots[1]);
        Assert.True(failed);
        vm.Dispose();
    }
}
