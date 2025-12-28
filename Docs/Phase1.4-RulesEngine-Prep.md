# Phase 1.4: Rules Engine - Préparation

**Status**: Ready to start
**Previous Phase**: 1.3 ✅ COMPLETE (30/30 tests passing)
**Target**: Rules Engine Implementation

---

## 📋 Aperçu Phase 1.4

### Objectif
Implémente un système de règles déterministes pour valider et guider les transitions d'état narratives.

### Principes
- **Déterminisme**: Mêmes règles + mêmes conditions = mêmes résultats
- **Composition**: Les règles se composent via des conditions
- **Immuabilité**: Les règles ne modifient pas l'état (pure)
- **Extensibilité**: Nouvelles règles sans modification du core

---

## 🎯 Composants à Implémenter

### 1. Rule Base Abstractions
```csharp
// Core/IRule.cs - Interface pour une règle
public interface IRule<TContext>
{
    string Name { get; }
    Result<Unit> Evaluate(TContext context);
}

// Domain/Condition - Abstraction pour conditions
public interface ICondition<TContext>
{
    bool IsSatisfied(TContext context);
}

// Domain/Effect - Abstraction pour effets
public interface IEffect<TContext>
{
    Result<TContext> Apply(TContext context);
}
```

### 2. Concrete Rules (Domain)
```csharp
// Examples:
- CharacterMustBeAliveRule: Cannot act if dead
- LocationExistsRule: Cannot move to non-existent location
- RelationshipConstraintRule: No self-relationships
- DeadCharacterNoRevelationRule: Dead can't learn things
- TimeProgressionRule: Time must be monotonic
```

### 3. Rule Engine (Simulation)
```csharp
public interface IRuleEngine
{
    Result<Unit> ValidateState(StoryState state);
    Result<Unit> ValidateTransition(StoryState before, StoryState after);
    Result<Unit> ValidateAction(StoryState state, StoryAction action);
    IEnumerable<RuleViolation> GetViolations(StoryState state);
}
```

### 4. Integration Points
- StateTransitionService: Appliquer rules avant application
- ProgressionService: Vérifier règles avant progression
- Tests: Valider règles avec Phase 1.3 entities

---

## 📊 Architecture Prévue

```
Domain/
├── Rules/
│   ├── IRule<T>
│   ├── ICondition<T>
│   ├── IEffect<T>
│   ├── RuleViolation
│   └── Built-in rules (7-10)

Simulation/
├── Rules/
│   ├── IRuleEngine
│   ├── RuleEngine
│   └── RuleValidator

Tests/
└── Phase1Step4RulesEngineTests.cs (15-20 tests)
```

---

## 🧪 Tests à Implémenter

### Validation Rules (5 tests)
1. DeadCharacter_CannotAct_ShouldFail
2. LocationDoesNotExist_ShouldFail
3. CharacterDoesNotExist_ShouldFail
4. TimeGoesBackward_ShouldFail
5. SelfRelationship_ShouldFail

### Rule Engine Integration (5 tests)
1. RuleEngine_ValidatesBeforeTransition
2. RuleEngine_ReturnsAllViolations
3. RuleEngine_DetectsInvariantViolations
4. MultipleRules_AllEvaluated
5. RuleEngine_Deterministic

### State Validation (5 tests)
1. InvalidState_DetectedImmediately
2. StateBecomesInvalid_AfterTransition
3. Rules_EnforcedConsistently
4. Violations_HaveClearMessages
5. Rules_DoNotModifyState

### Integration with Phase 1.3 (5 tests)
1. StateTransitionService_IntegratesRuleEngine
2. ProgressionService_ChecksRules
3. Rules_PreventInvalidActions
4. Rules_AllowValidActions
5. CompleteFlow_WithRuleEnforcement

---

## 🔍 Règles Narratives à Implémenter

### Character Rules
- ✅ Dead characters cannot move
- ✅ Dead characters cannot participate in encounters
- ✅ Dead characters cannot learn (revelations)
- ✅ Character must exist to act

### Location Rules
- ✅ Destination location must exist
- ✅ Cannot stay in same location (move to different)
- ✅ Location hierarchy must be valid

### Relationship Rules
- ✅ No self-relationships (A cannot relate to A)
- ✅ Relationships must be symmetric
- ✅ Both characters must exist

### Time Rules
- ✅ Time must advance monotonically (never go backward)
- ✅ Negative durations forbidden
- ✅ Time progression recorded

### Event Rules
- ✅ Events are immutable once created
- ✅ Event order must match application order
- ✅ No events created for no reason

---

## 🏗️ Design Decisions to Make

1. **Rule Composition Strategy**
   - Option A: Chain of Responsibility (sequential)
   - Option B: Composite Pattern (tree structure)
   - Option C: RuleSet composition
   - **Recommendation**: Option A (simpler, deterministic)

2. **Violation Reporting**
   - Return first violation or all violations?
   - Include rule context?
   - Severity levels?
   - **Recommendation**: Return all violations for transparency

3. **Rule Application Timing**
   - Before action? After? Both?
   - Validate state consistency?
   - **Recommendation**: Before (fail-fast), after (invariants)

4. **Rule Extensibility**
   - Predefined rules only or custom rules?
   - User-defined rules?
   - Plugin architecture?
   - **Recommendation**: Predefined for Phase 1.4, extensible for later

5. **Performance**
   - Cache rule results?
   - Short-circuit evaluation?
   - Parallel rule execution?
   - **Recommendation**: Keep simple, add optimization later

---

## 📋 Implementation Checklist

### Code Implementation
- [ ] IRule<T> interface
- [ ] ICondition<T> interface
- [ ] IEffect<T> interface
- [ ] RuleViolation record
- [ ] 8-10 concrete rule implementations
- [ ] IRuleEngine interface
- [ ] RuleEngine implementation
- [ ] Integration with StateTransitionService

### Testing
- [ ] 15-20 comprehensive tests
- [ ] All tests passing
- [ ] Edge cases covered
- [ ] Integration verified

### Documentation
- [ ] Update Phase1.md
- [ ] Create Step1.4-RulesEngine-DONE.md
- [ ] Document rules in Phase1-Design.md
- [ ] Update ROADMAP.md

### Quality Assurance
- [ ] 0 compilation errors
- [ ] 0 warnings
- [ ] All Phase 1.2 tests still passing (17)
- [ ] All Phase 1.3 tests still passing (13)
- [ ] New Phase 1.4 tests passing (15-20)
- [ ] **Total: 45-50 tests all passing**

---

## 🚀 Estimation

| Task | Hours | Priority |
|------|-------|----------|
| Design | 1 | High |
| Core abstractions | 2 | High |
| Concrete rules | 3 | High |
| Rule engine | 2 | High |
| Integration | 2 | High |
| Tests | 4 | High |
| Documentation | 2 | Medium |
| **TOTAL** | **16** | **~1 day** |

---

## 🔗 Dependencies

### What Phase 1.4 Depends On
- ✅ Phase 1.2: Core & Domain (complete)
- ✅ Phase 1.3: State Management (complete)
- ✅ Current testing framework (xUnit)

### What Depends on Phase 1.4
- Phase 1.5: Persistence (can use rules for validation)
- Phase 2+: Advanced features

---

## 📚 References

### From Phase 1.3
- `StateTransitionService`: Integration point
- `IStateTransitionService`: Interface to extend
- `StoryState`: Context for rule evaluation
- `Result<T>`: Error handling pattern

### From Phase 1.2
- Domain entities (Character, Location, etc.)
- Event hierarchy
- Relationship constraints

### To Design
- Rule composition strategy
- Violation reporting format
- Rule configuration options

---

## 🎯 Success Criteria

1. ✅ **Determinism**: Same rules + state → same result always
2. ✅ **Completeness**: All narrative invariants enforced
3. ✅ **Clarity**: Rule violations have clear messages
4. ✅ **Performance**: Rules evaluated in < 1ms
5. ✅ **Integration**: Works seamlessly with Phase 1.3
6. ✅ **Testing**: 100% test pass rate
7. ✅ **Documentation**: Clear rule documentation

---

## 🚦 Ready to Start?

**Previous Phases**: ✅ COMPLETE (30/30 tests)
**Build Status**: ✅ SUCCESS
**Documentation**: ✅ UP TO DATE

**Next Command**: `Développe l'étape 1.4` or `Développe le Rules Engine`

---

**Phase 1.4 Preparation**: ✅ READY
**Phase 1.3 Status**: ✅ COMPLETE
**Build Status**: ✅ SUCCESS (0 errors, 0 warnings)
