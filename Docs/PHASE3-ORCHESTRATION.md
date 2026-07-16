# Phase 3 — Orchestration Multi-Agents

**Status**: ✅ COMPLÈTE  
**Phase**: Phase 3 — Orchestration & Agents  
**Dependencies**: Phase 1 (✅ COMPLETE), Phase 2 (✅ COMPLETE)  
**Date de finalisation**: Décembre 2025

---

## 📋 Vue d'ensemble

Phase 3 implémente un **système d'orchestration multi-agents** qui coordonne plusieurs agents IA spécialisés pour créer des histoires cohérentes et riches. Le système fonctionne comme un pipeline où chaque agent a une responsabilité spécifique.

### Objectifs Atteints

✅ **Pipeline Multi-Agents** - Coordination de 4 agents spécialisés  
✅ **Système de Prompts** - Templates structurés et localisés  
✅ **Gestion Temporelle** - TimeProvider pour cohérence narrative  
✅ **Localisation** - Support FR/EN complet  
✅ **Skills System** - Capacités narratives modulaires  
✅ **Tests Complets** - Suite de tests Orchestration.Tests  

---

## 🏗️ Architecture

### Modules

```
Narratum.Orchestration/
├── Agents/
│   ├── INarrativeAgent.cs          # Interface base agent
│   ├── NarratorAgent.cs            # Génération texte narratif
│   ├── CharacterAgent.cs           # Dialogues et réactions
│   ├── SummaryAgent.cs             # Résumés et compression
│   └── ConsistencyAgent.cs         # Vérification cohérence
│
├── Pipeline/
│   ├── OrchestrationPipeline.cs    # Pipeline principal
│   ├── PipelineContext.cs          # Contexte d'exécution
│   └── PipelineStep.cs             # Étape de pipeline
│
├── Prompts/
│   ├── PromptTemplates.cs          # Templates de prompts
│   ├── PromptBuilder.cs            # Construction prompts
│   └── Localization/
│       ├── PromptsFR.resx          # Prompts français
│       └── PromptsEN.resx          # Prompts anglais
│
├── Models/
│   ├── NarrativeAgentResponse.cs   # Réponse d'agent
│   ├── AgentContext.cs             # Contexte pour agents
│   └── Skills/
│       ├── ISkill.cs               # Interface skill
│       └── SkillRegistry.cs        # Registre des skills
│
└── Services/
    ├── OrchestrationService.cs     # Service principal
    └── TimeProvider.cs             # Gestion temps narratif
```

---

## 🤖 Les 4 Agents Narratifs

### 1. NarratorAgent

**Responsabilité** : Génération du texte narratif principal

**Entrée** :
- État actuel du monde
- Événements à narrer
- Contexte historique

**Sortie** :
- Texte narratif riche et descriptif
- Ambiance et atmosphère
- Descriptions de scènes

**Exemple** :
```csharp
var response = await narratorAgent.GenerateAsync(new AgentContext
{
    WorldState = currentState,
    Events = newEvents,
    PreviousContext = history
});

// response.Content: "Le soleil se couchait sur la forêt de Silvermist..."
```

### 2. CharacterAgent

**Responsabilité** : Dialogues et réactions des personnages

**Entrée** :
- Personnage ciblé
- Situation actuelle
- Relations avec autres personnages

**Sortie** :
- Dialogues authentiques
- Réactions émotionnelles
- Actions du personnage

**Exemple** :
```csharp
var response = await characterAgent.GenerateDialogueAsync(new CharacterContext
{
    Character = aric,
    Situation = "découverte de la carte ancienne",
    OtherCharacters = [lyra, kael]
});

// response.Dialogue: "Cette carte... elle mène aux Cavernes de Cristal!"
```

### 3. SummaryAgent

**Responsabilité** : Résumés factuels et compression d'historique

**Entrée** :
- Historique d'événements
- Niveau de granularité (Event, Chapter, Arc, World)

**Sortie** :
- Résumé factuel concis
- Points clés extraits
- Pas d'embellissement narratif

**Caractéristiques** :
- **Température basse** (0.1-0.2) pour déterminisme
- **Format structuré** pour parsing
- **Compression intelligente**

**Exemple** :
```csharp
var summary = await summaryAgent.SummarizeAsync(new SummaryContext
{
    Events = last50Events,
    Level = MemoryLevel.Chapter
});

// summary.Content: "Aric découvre carte. Lyra alliée. Trio vers cavernes."
```

### 4. ConsistencyAgent

**Responsabilité** : Vérification de cohérence narrative

**Entrée** :
- État canonique actuel
- Nouveau contenu proposé

**Sortie** :
- Violations détectées
- Suggestions de correction
- Score de cohérence

**Validations** :
- Pas de contradictions factuelles
- Continuité temporelle
- Cohérence des personnages
- Respect des règles du monde

**Exemple** :
```csharp
var validation = await consistencyAgent.ValidateAsync(new ValidationContext
{
    CanonicalState = currentMemory,
    ProposedContent = newNarrative
});

if (!validation.IsValid)
{
    // validation.Violations: ["Aric est mort au chapitre 3 mais parle ici"]
}
```

---

## 🔄 Pipeline d'Orchestration

### Flux Standard

```
1. OrchestrationPipeline.StartAsync()
   ↓
2. PipelineContext créé avec état initial
   ↓
3. Pour chaque événement narratif:
   │
   ├─→ NarratorAgent génère texte narratif
   │   ↓
   ├─→ CharacterAgent génère dialogues
   │   ↓
   ├─→ ConsistencyAgent valide cohérence
   │   ↓
   └─→ SummaryAgent résume si nécessaire
   ↓
4. PipelineContext enrichi retourné
```

### Code Exemple

```csharp
public class OrchestrationService
{
    public async Task<NarrativeResult> GenerateNarrativeAsync(
        StoryState currentState,
        IEnumerable<Event> newEvents)
    {
        var context = new PipelineContext
        {
            State = currentState,
            Events = newEvents,
            Timestamp = _timeProvider.GetUtcNow()
        };

        // Étape 1: Génération narrative
        var narrative = await _narratorAgent.GenerateAsync(context);
        context.AddNarrative(narrative);

        // Étape 2: Génération dialogues
        foreach (var character in GetActiveCharacters(newEvents))
        {
            var dialogue = await _characterAgent.GenerateDialogueAsync(
                context, character);
            context.AddDialogue(character, dialogue);
        }

        // Étape 3: Validation cohérence
        var validation = await _consistencyAgent.ValidateAsync(context);
        if (!validation.IsValid)
        {
            // Retry ou correction
            context.AddViolations(validation.Violations);
        }

        // Étape 4: Résumé si nécessaire
        if (ShouldSummarize(context))
        {
            var summary = await _summaryAgent.SummarizeAsync(context);
            context.AddSummary(summary);
        }

        return context.ToNarrativeResult();
    }
}
```

---

## 🌍 Localisation (FR/EN)

### Système de Prompts Localisés

Le système utilise des **Resource Files** (.resx) pour gérer les prompts dans plusieurs langues.

**Structure** :
```
Prompts/
├── Localization/
│   ├── PromptsFR.resx       # Français
│   ├── PromptsEN.resx       # Anglais
│   └── PromptsEN.Designer.cs
```

### Exemples de Prompts

**Français** (`PromptsFR.resx`):
```xml
<data name="NarratorSystemPrompt">
  <value>Tu es un narrateur expert en fantasy. Écris des textes riches et immersifs...</value>
</data>

<data name="CharacterDialoguePrompt">
  <value>Génère un dialogue authentique pour {0} dans la situation suivante...</value>
</data>
```

**Anglais** (`PromptsEN.resx`):
```xml
<data name="NarratorSystemPrompt">
  <value>You are an expert fantasy narrator. Write rich and immersive text...</value>
</data>

<data name="CharacterDialoguePrompt">
  <value>Generate authentic dialogue for {0} in the following situation...</value>
</data>
```

### Utilisation

```csharp
public class PromptBuilder
{
    private readonly CultureInfo _culture;

    public string BuildNarratorPrompt(AgentContext context)
    {
        // Sélectionne automatiquement FR ou EN selon _culture
        var template = PromptResources.NarratorSystemPrompt;
        return string.Format(template, context.WorldName, context.CurrentChapter);
    }
}
```

---

## ⏰ TimeProvider - Gestion du Temps Narratif

### Problème Résolu

Dans un système narratif, le temps peut être :
- **Temps réel** : Quand l'événement est créé
- **Temps narratif** : Quand l'événement se passe dans l'histoire

**TimeProvider** permet de :
- Synchroniser temps réel et temps narratif
- Tester avec temps contrôlé
- Gérer décalages temporels

### Interface

```csharp
public interface ITimeProvider
{
    DateTime GetUtcNow();
    DateTime GetNarrativeTime();
    void AdvanceNarrativeTime(TimeSpan duration);
}
```

### Utilisation

```csharp
public class OrchestrationService
{
    private readonly ITimeProvider _timeProvider;

    public async Task ProcessEventAsync(Event @event)
    {
        var timestamp = _timeProvider.GetUtcNow();
        var narrativeTime = _timeProvider.GetNarrativeTime();

        var context = new PipelineContext
        {
            RealTime = timestamp,
            NarrativeTime = narrativeTime,
            Event = @event
        };

        // Avancer le temps narratif
        _timeProvider.AdvanceNarrativeTime(TimeSpan.FromHours(2));
    }
}
```

---

## 🎯 Skills System

### Concept

Les **Skills** sont des capacités narratives modulaires que les agents peuvent utiliser.

### Exemples de Skills

```csharp
public interface ISkill
{
    string Name { get; }
    string Description { get; }
    Task<SkillResult> ExecuteAsync(SkillContext context);
}

public class DescribeLocationSkill : ISkill
{
    public string Name => "describe-location";
    
    public async Task<SkillResult> ExecuteAsync(SkillContext context)
    {
        var location = context.Get<Location>("location");
        var description = await GenerateDescriptionAsync(location);
        return SkillResult.Success(description);
    }
}

public class GenerateDialogueSkill : ISkill
{
    public string Name => "generate-dialogue";
    
    public async Task<SkillResult> ExecuteAsync(SkillContext context)
    {
        var character = context.Get<Character>("character");
        var situation = context.Get<string>("situation");
        var dialogue = await GenerateDialogueAsync(character, situation);
        return SkillResult.Success(dialogue);
    }
}
```

### Registre de Skills

```csharp
public class SkillRegistry
{
    private readonly Dictionary<string, ISkill> _skills = new();

    public void RegisterSkill(ISkill skill)
    {
        _skills[skill.Name] = skill;
    }

    public ISkill? GetSkill(string name)
    {
        return _skills.TryGetValue(name, out var skill) ? skill : null;
    }

    public IEnumerable<ISkill> GetAllSkills() => _skills.Values;
}
```

---

## 📊 NarrativeAgentResponse

### Modèle de Réponse

```csharp
public record NarrativeAgentResponse
{
    public string Content { get; init; }
    public AgentType AgentType { get; init; }
    public DateTime Timestamp { get; init; }
    public Dictionary<string, object> Metadata { get; init; }
    public double ConfidenceScore { get; init; }
    public List<string> Tags { get; init; }
    
    public bool IsValid => !string.IsNullOrWhiteSpace(Content);
}

public enum AgentType
{
    Narrator,
    Character,
    Summary,
    Consistency
}
```

### Utilisation

```csharp
var response = await narratorAgent.GenerateAsync(context);

if (response.IsValid)
{
    Console.WriteLine($"[{response.AgentType}] {response.Content}");
    Console.WriteLine($"Confidence: {response.ConfidenceScore:P0}");
    Console.WriteLine($"Tags: {string.Join(", ", response.Tags)}");
}
```

---

## 🧪 Tests

### Structure des Tests

```
Orchestration.Tests/
├── AgentTests/
│   ├── NarratorAgentTests.cs
│   ├── CharacterAgentTests.cs
│   ├── SummaryAgentTests.cs
│   └── ConsistencyAgentTests.cs
│
├── PipelineTests/
│   ├── OrchestrationPipelineTests.cs
│   └── PipelineContextTests.cs
│
├── PromptTests/
│   ├── PromptBuilderTests.cs
│   └── LocalizationTests.cs
│
└── IntegrationTests/
    └── EndToEndOrchestrationTests.cs
```

### Exemples de Tests

```csharp
[Fact]
public async Task NarratorAgent_GeneratesValidNarrative()
{
    // Arrange
    var agent = new NarratorAgent(_mockLlm.Object, _promptBuilder);
    var context = CreateTestContext();

    // Act
    var response = await agent.GenerateAsync(context);

    // Assert
    response.Should().NotBeNull();
    response.IsValid.Should().BeTrue();
    response.AgentType.Should().Be(AgentType.Narrator);
    response.Content.Should().NotBeEmpty();
}

[Fact]
public async Task OrchestrationPipeline_ProcessesMultipleAgents()
{
    // Arrange
    var pipeline = CreatePipeline();
    var events = CreateTestEvents(5);

    // Act
    var result = await pipeline.ProcessAsync(events);

    // Assert
    result.Should().NotBeNull();
    result.NarrativeTexts.Should().HaveCount(5);
    result.Dialogues.Should().NotBeEmpty();
    result.Validations.Should().AllSatisfy(v => v.IsValid.Should().BeTrue());
}
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Orchestration": {
    "DefaultCulture": "fr-FR",
    "EnableSkills": true,
    "Pipeline": {
      "MaxRetries": 3,
      "TimeoutSeconds": 30,
      "ParallelAgents": false
    },
    "Agents": {
      "Narrator": {
        "Temperature": 0.7,
        "MaxTokens": 500
      },
      "Character": {
        "Temperature": 0.8,
        "MaxTokens": 200
      },
      "Summary": {
        "Temperature": 0.1,
        "MaxTokens": 150
      },
      "Consistency": {
        "Temperature": 0.0,
        "MaxTokens": 100
      }
    }
  }
}
```

### Injection de Dépendances

```csharp
public static IServiceCollection AddOrchestration(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.Configure<OrchestrationOptions>(
        configuration.GetSection("Orchestration"));

    services.AddSingleton<ITimeProvider, SystemTimeProvider>();
    services.AddSingleton<SkillRegistry>();
    
    services.AddScoped<INarrativeAgent, NarratorAgent>();
    services.AddScoped<INarrativeAgent, CharacterAgent>();
    services.AddScoped<INarrativeAgent, SummaryAgent>();
    services.AddScoped<INarrativeAgent, ConsistencyAgent>();
    
    services.AddScoped<OrchestrationPipeline>();
    services.AddScoped<OrchestrationService>();

    return services;
}
```

---

## 📈 Métriques

| Métrique | Valeur |
|----------|--------|
| **Fichiers créés** | ~30 fichiers |
| **Lignes de code** | ~3,000 lignes |
| **Tests** | ~50 tests |
| **Agents** | 4 agents spécialisés |
| **Skills** | 10+ skills |
| **Langues** | FR + EN |
| **Coverage** | ~85% |

---

## 🎯 Points Clés

### Succès

1. ✅ **Séparation des responsabilités** - Chaque agent a un rôle clair
2. ✅ **Extensibilité** - Facile d'ajouter nouveaux agents
3. ✅ **Testabilité** - Pipeline et agents testables indépendamment
4. ✅ **Localisation** - Support multilingue natif
5. ✅ **Configuration** - Paramètres ajustables par agent

### Défis Résolus

1. ✅ **Coordination agents** - Pipeline garantit ordre d'exécution
2. ✅ **Gestion du temps** - TimeProvider pour cohérence
3. ✅ **Prompts multilingues** - Resource files + CultureInfo
4. ✅ **Validation cohérence** - Agent dédié avant commit

---

## 🚀 Utilisation

### Exemple Complet

```csharp
public class NarrativeGenerator
{
    private readonly OrchestrationService _orchestration;

    public async Task<string> GenerateChapterAsync(
        StoryState state,
        IEnumerable<Event> chapterEvents)
    {
        // Orchestrer génération
        var result = await _orchestration.GenerateNarrativeAsync(
            state, 
            chapterEvents);

        // Compiler résultat
        var narrative = new StringBuilder();
        
        // Texte narratif
        narrative.AppendLine(result.NarrativeText);
        narrative.AppendLine();
        
        // Dialogues
        foreach (var dialogue in result.Dialogues)
        {
            narrative.AppendLine($"\"{dialogue.Text}\" - {dialogue.Character}");
        }
        
        // Résumé si disponible
        if (result.Summary != null)
        {
            narrative.AppendLine();
            narrative.AppendLine($"Résumé: {result.Summary}");
        }

        return narrative.ToString();
    }
}
```

---

## 📝 Prochaines Améliorations

### Court Terme
- [ ] Optimiser prompts pour meilleure qualité
- [ ] Ajouter cache de réponses
- [ ] Métriques de performance agents

### Moyen Terme
- [ ] Support plus de langues (ES, DE)
- [ ] Skills marketplace
- [ ] Dashboard monitoring agents

---

**Phase 3 pose les bases de l'orchestration IA pour Narratum. Les agents travaillent ensemble pour créer des histoires cohérentes, riches et immersives.** ✨

---

**Dernière mise à jour** : 16 Juillet 2026  
**Statut** : ✅ COMPLÈTE
