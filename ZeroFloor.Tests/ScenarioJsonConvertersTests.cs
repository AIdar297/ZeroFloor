using System.Text.Json;
using ZeroFloor.Models;
using ZeroFloor.Services.Json;

namespace ZeroFloor.Tests;

public class ScenarioJsonConvertersTests
{
    private static readonly JsonSerializerOptions Options = ScenarioJsonOptions.Create();

    [Fact]
    public void Effect_SetFlag_RoundTrips()
    {
        const string json = """{"type":"setFlag","key":"father_file_found","value":true}""";

        var effect = JsonSerializer.Deserialize<Effect>(json, Options)!;

        Assert.Equal(EffectType.SetFlag, effect.Type);
        Assert.Equal("father_file_found", effect.Key);
        Assert.Equal(FlagValue.FromBoolean(true), effect.FlagValue);

        var writtenBack = JsonSerializer.Serialize(effect, Options);
        var reparsed = JsonSerializer.Deserialize<Effect>(writtenBack, Options)!;
        Assert.Equal(effect.FlagValue, reparsed.FlagValue);
    }

    [Fact]
    public void Effect_AddStat_RoundTrips()
    {
        const string json = """{"type":"addStat","key":"K","value":-15}""";

        var effect = JsonSerializer.Deserialize<Effect>(json, Options)!;

        Assert.Equal(EffectType.AddStat, effect.Type);
        Assert.Equal("K", effect.Key);
        Assert.Equal(-15, effect.NumericValue);
    }

    [Fact]
    public void Effect_MissingValue_ThrowsJsonException()
    {
        const string json = """{"type":"addStat","key":"K"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Effect>(json, Options));
    }

    [Theory]
    [InlineData(">", ComparisonOperator.GreaterThan)]
    [InlineData(">=", ComparisonOperator.GreaterThanOrEqual)]
    [InlineData("<", ComparisonOperator.LessThan)]
    [InlineData("<=", ComparisonOperator.LessThanOrEqual)]
    [InlineData("=", ComparisonOperator.Equal)]
    public void ComparisonOperator_ParsesSymbol(string symbol, ComparisonOperator expected)
    {
        var json = $$"""{"type":"statCompare","stat":"K","operator":"{{symbol}}","value":40}""";

        var condition = (StatComparisonCondition)JsonSerializer.Deserialize<ConditionNode>(json, Options)!;

        Assert.Equal(expected, condition.Operator);
        Assert.Equal(StatId.K, condition.Stat);
        Assert.Equal(40, condition.Value);
    }

    [Fact]
    public void ConditionNode_PolymorphicDeserialization_ResolvesCorrectSubtype()
    {
        const string json = """
            {
                "type": "and",
                "children": [
                    { "type": "statCompare", "stat": "H", "operator": ">=", "value": 70 },
                    { "type": "flagEquals", "flagKey": "father_file_found", "value": true },
                    { "type": "not", "child": { "type": "flagEquals", "flagKey": "sonya_exposed", "value": true } }
                ]
            }
            """;

        var condition = JsonSerializer.Deserialize<ConditionNode>(json, Options);

        var and = Assert.IsType<AndCondition>(condition);
        Assert.Equal(3, and.Children.Count);
        Assert.IsType<StatComparisonCondition>(and.Children[0]);
        Assert.IsType<FlagEqualsCondition>(and.Children[1]);
        Assert.IsType<NotCondition>(and.Children[2]);
    }

    [Fact]
    public void SceneDefinition_FullNode_DeserializesWithCamelCaseProperties()
    {
        const string json = """
            [
              {
                "sceneId": "S1",
                "background": "bg1",
                "nodes": [
                  {
                    "nodeId": "N1",
                    "speaker": "max",
                    "text": "Привет.",
                    "choices": [
                      {
                        "choiceId": "C1",
                        "text": "Вариант",
                        "condition": { "type": "statCompare", "stat": "K", "operator": ">=", "value": 40 },
                        "effects": [ { "type": "addStat", "key": "K", "value": 10 } ],
                        "nextNodeId": "N1"
                      }
                    ]
                  }
                ]
              }
            ]
            """;

        var scenes = JsonSerializer.Deserialize<SceneDefinition[]>(json, Options)!;

        Assert.Single(scenes);
        Assert.Equal("S1", scenes[0].SceneId);
        Assert.Equal("N1", scenes[0].Nodes[0].NodeId);
        Assert.Equal("C1", scenes[0].Nodes[0].Choices[0].ChoiceId);
        Assert.IsType<StatComparisonCondition>(scenes[0].Nodes[0].Choices[0].Condition);
    }
}
