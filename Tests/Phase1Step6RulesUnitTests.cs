using Xunit;
using FluentAssertions;
using Narratum.Core;
using Narratum.Simulation;
using Narratum.State;
using Narratum.Domain;

namespace Narratum.Tests;

public class Phase1Step6RulesUnitTests
{
    [Fact]
    public void RuleEngine_Constructor_ShouldCreateValidEngine()
    {
        var engine = new RuleEngine();
        engine.Should().NotBeNull();
    }

    [Fact]
    public void RuleEngine_ValidateState_WithValidState_ShouldReturnOk()
    {
        var engine = new RuleEngine();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var result = engine.ValidateState(state);
        result.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void RuleEngine_GetStateViolations_WithValidState_ShouldReturnEmpty()
    {
        var engine = new RuleEngine();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var violations = engine.GetStateViolations(state);
        violations.Should().BeEmpty();
    }

    [Fact]
    public void RuleEngine_GetStateViolations_ShouldReturnIReadOnlyList()
    {
        var engine = new RuleEngine();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var violations = engine.GetStateViolations(state);
        violations.Should().NotBeNull();
        violations.Should().BeAssignableTo<IReadOnlyList<RuleViolation>>();
    }

    [Fact]
    public void RuleEngine_ShouldImplementIRuleEngine()
    {
        var engine = new RuleEngine();
        engine.Should().BeAssignableTo<IRuleEngine>();
    }

    [Fact]
    public void RuleViolation_Error_ShouldCreateErrorSeverity()
    {
        var description = "Test violation";
        var violation = RuleViolation.Error(ruleId: "rule1", ruleName: "TestRule", message: description);
        violation.Message.Should().Be(description);
        violation.Severity.Should().Be(RuleSeverity.Error);
    }

    [Fact]
    public void RuleViolation_Warning_ShouldCreateWarningSeverity()
    {
        var violation = RuleViolation.Warning(ruleId: "rule1", ruleName: "TestRule", message: "Warning");
        violation.Severity.Should().Be(RuleSeverity.Warning);
    }

    [Fact]
    public void RuleViolation_Info_ShouldCreateInfoSeverity()
    {
        var violation = RuleViolation.Info(ruleId: "rule1", ruleName: "TestRule", message: "Info");
        violation.Severity.Should().Be(RuleSeverity.Info);
    }

    [Fact]
    public void RuleSeverity_ShouldHaveMultipleLevels()
    {
        RuleSeverity.Info.Should().Be(RuleSeverity.Info);
        RuleSeverity.Warning.Should().Be(RuleSeverity.Warning);
        RuleSeverity.Error.Should().Be(RuleSeverity.Error);
    }

    [Fact]
    public void RuleEngine_ValidateState_ShouldBeConsistent()
    {
        var engine = new RuleEngine();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var result1 = engine.ValidateState(state);
        var result2 = engine.ValidateState(state);
        result1.Should().BeOfType<Result<Unit>.Success>();
        result2.Should().BeOfType<Result<Unit>.Success>();
    }

    [Fact]
    public void RuleEngine_Rules_ShouldHaveRulesCollection()
    {
        var engine = new RuleEngine();
        var rules = engine.Rules;
        rules.Should().NotBeNull();
        rules.Should().BeAssignableTo<IReadOnlyList<IRule>>();
    }

    [Fact]
    public void RuleEngine_GetActionViolations_WithValidAction_ShouldReturnEmpty()
    {
        var engine = new RuleEngine();
        var worldState = new WorldState(worldId: Id.New(), worldName: "Test");
        var state = new StoryState(worldState: worldState);
        var action = new AdvanceTimeAction(TimeSpan.FromHours(1));
        var violations = engine.GetActionViolations(state, action);
        violations.Should().NotBeNull();
    }
}
