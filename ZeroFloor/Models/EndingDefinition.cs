namespace ZeroFloor.Models;

public sealed class EndingDefinition
{
    public required string EndingId { get; init; }
    public required string BranchId { get; init; }
    public required string StartingSceneId { get; init; }
}
