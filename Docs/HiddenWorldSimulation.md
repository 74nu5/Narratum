# Hidden World Simulation

## Concept : Événements cachés et simulation hors-scène

### Vision

Transformer le moteur narratif d'un "générateur d'histoires" en un "monde vivant" où les événements continuent même hors de la vue du protagoniste ou du lecteur.

---

## 🧠 Les trois catégories d'événements cachés

### 1. Événements du monde hors caméra
Événements qui se produisent ailleurs dans l'univers narratif :
- Guerres lointaines
- Famines et catastrophes naturelles
- Décisions politiques
- Complots en cours
- Changements climatiques ou saisonniers

### 2. Évolution des personnages hors scène
Changements qui affectent les personnages non présents :
- Entraînement et amélioration de compétences
- Maladies et guérisons
- Vieillissement
- Changements de relations
- Voyages et déplacements

### 3. États internes non exposés
Informations existantes mais non révélées :
- Pensées et réflexions
- Intentions et motivations
- Peurs et doutes
- Mensonges et secrets
- Plans cachés

---

## ❌ Erreur à éviter

**NE PAS** créer un agent IA qui "imagine ce qui se passe ailleurs"

Pourquoi c'est destructeur :
- ❌ Perte du déterminisme
- ❌ Incohérence du monde
- ❌ Code non testable
- ❌ Magie opaque et imprévisible

---

## ✅ La bonne approche : Système déterministe (Phase 1-2)

### Nom du système

**HiddenWorldSimulation** ou **OffSceneSimulationService**

### Principe

> **PAS un agent IA, MAIS un système de règles déterministes**

---

## 🏗️ Architecture

### Rôle exact

Le système :
- S'exécute **entre deux chapitres** ou à intervalles narratifs définis
- Produit des **HiddenEvent** (événements cachés)
- Modifie les **HiddenState** (états cachés)
- Respecte strictement les règles du monde
- **Ne génère JAMAIS de texte** (Phase 1-2)

---

## 📦 Nouveaux concepts à introduire

### 1. HiddenEvent

Extension de `StoryEvent` avec niveau de visibilité.

```csharp
public sealed class HiddenEvent : StoryEvent
{
    public Guid Id { get; init; }
    public EventType Type { get; init; }
    public DateTime NarrativeTimestamp { get; init; }
    public VisibilityLevel Visibility { get; init; }
    public List<Guid> ActorIds { get; init; }
    public Guid? LocationId { get; init; }
    public Dictionary<string, object> Data { get; init; }
    
    // Métadonnées de révélation
    public DateTime? RevealedAt { get; set; }
    public RevealMethod? RevealedHow { get; set; }
}
```

### 2. VisibilityLevel

Niveaux de visibilité d'un événement.

```csharp
public enum VisibilityLevel
{
    /// <summary>
    /// Événement complètement caché, non révélé au lecteur
    /// </summary>
    Hidden = 0,
    
    /// <summary>
    /// Indices suggérés, préfiguré sans détails complets
    /// </summary>
    Foreshadowed = 1,
    
    /// <summary>
    /// Événement révélé au lecteur (flashback, découverte, etc.)
    /// </summary>
    Revealed = 2
}
```

### 3. RevealMethod

Comment un événement caché est révélé.

```csharp
public enum RevealMethod
{
    DirectNarration,      // Narration directe
    CharacterDiscovery,   // Découverte par un personnage
    Flashback,            // Flashback narratif
    Dialogue,             // Révélé dans un dialogue
    Document,             // Trouvé dans un document/lettre
    Rumor,                // Rumeur ou information partielle
    Observation           // Observation directe des conséquences
}
```

### 4. InternalCharacterState

État interne d'un personnage (pensées, émotions, intentions).

```csharp
public class InternalCharacterState
{
    public Guid CharacterId { get; init; }
    
    /// <summary>
    /// État émotionnel actuel
    /// </summary>
    public EmotionalState CurrentEmotion { get; set; }
    
    /// <summary>
    /// Intentions et objectifs cachés
    /// </summary>
    public List<Intention> Intentions { get; init; }
    
    /// <summary>
    /// Secrets connus uniquement du personnage
    /// </summary>
    public List<Secret> Secrets { get; init; }
    
    /// <summary>
    /// Plans en cours non révélés
    /// </summary>
    public List<HiddenPlan> Plans { get; init; }
    
    /// <summary>
    /// Pensées et réflexions internes
    /// </summary>
    public List<InternalThought> Thoughts { get; init; }
}
```

```csharp
public class EmotionalState
{
    public EmotionType Dominant { get; set; }
    public int Intensity { get; set; } // 0-100
    public DateTime Since { get; set; }
}

public enum EmotionType
{
    Neutral,
    Joy,
    Sadness,
    Anger,
    Fear,
    Surprise,
    Disgust,
    Trust,
    Anticipation
}
```

```csharp
public class Intention
{
    public Guid Id { get; init; }
    public string Goal { get; init; }
    public IntentionPriority Priority { get; init; }
    public List<Guid> RequiredResourceIds { get; init; }
    public DateTime CreatedAt { get; init; }
}

public enum IntentionPriority
{
    Low,
    Medium,
    High,
    Critical
}
```

```csharp
public class Secret
{
    public Guid Id { get; init; }
    public string Content { get; init; }
    public SecretSeverity Severity { get; init; }
    public List<Guid> KnownByCharacterIds { get; init; }
    public bool CanBeRevealed { get; set; }
}

public enum SecretSeverity
{
    Minor,
    Moderate,
    Major,
    WorldChanging
}
```

---

## 🔧 OffSceneSimulationService

### Responsabilités

Le service qui gère la simulation hors-scène :

1. Faire évoluer le monde "en arrière-plan"
2. Appliquer des règles globales temporelles
3. Déclencher des événements invisibles mais canoniques
4. Progresser les plans et intentions des personnages

### Interface

```csharp
public interface IOffSceneSimulationService
{
    /// <summary>
    /// Simule les événements cachés entre deux moments narratifs
    /// </summary>
    SimulationResult SimulateHiddenWorld(
        StoryState currentState,
        TimeSpan narrativeTimePassed);
    
    /// <summary>
    /// Révèle un événement caché selon une méthode spécifique
    /// </summary>
    RevealResult RevealHiddenEvent(
        Guid hiddenEventId,
        RevealMethod method,
        Guid? revealerCharacterId = null);
    
    /// <summary>
    /// Évalue les intentions des personnages et progresse leurs plans
    /// </summary>
    List<HiddenEvent> ProgressCharacterIntentions(
        StoryState currentState,
        List<Guid> characterIds);
}
```

### Exemples de simulation

```csharp
// Exemple 1 : Entraînement hors-scène
var hiddenEvent = new HiddenEvent
{
    Type = EventType.SkillImprovement,
    Visibility = VisibilityLevel.Hidden,
    ActorIds = [mentorCharacterId],
    Data = new Dictionary<string, object>
    {
        ["skill"] = "Swordmanship",
        ["improvement"] = 5,
        ["duration_days"] = 30
    }
};

// Exemple 2 : Complot qui progresse
var plotEvent = new HiddenEvent
{
    Type = EventType.PlotProgression,
    Visibility = VisibilityLevel.Foreshadowed, // Indices donnés
    ActorIds = [villain1Id, villain2Id],
    LocationId = secretBaseId,
    Data = new Dictionary<string, object>
    {
        ["plot_name"] = "Assassination Plan",
        ["progress"] = 75, // %
        ["next_step"] = "Infiltrate castle"
    }
};
```

---

## 🧠 Règles critiques

### 🔒 Séparation stricte

**Principe fondamental :**
- **Visible ≠ Vrai**
- **Caché ≠ Faux**

Le moteur connaît la vérité absolue.
Le lecteur ne connaît qu'une projection partielle.

### 🔁 Révélation différée

Un événement caché peut évoluer :
- `Hidden` → `Foreshadowed` (indices narratifs)
- `Foreshadowed` → `Revealed` (révélation complète)

Possibilités narratives :
- **Dramatic irony** : Le lecteur sait, le héros ne sait pas
- **Twist** : Révélation d'un événement caché 20 chapitres plus tard
- **Suspense** : Indices progressifs d'un danger
- **Flashback** : Révélation d'événements passés

---

## 📅 Intégration par phase

### Phase 1 (Fondations - ACTUELLE)

**Inclus :**
- ✅ Modèle de données (`HiddenEvent`, `InternalCharacterState`)
- ✅ États cachés dans `StoryState`
- ✅ Simulation hors scène déterministe
- ✅ Règles de progression des événements cachés

**Exclus :**
- ❌ IA pour générer du contenu
- ❌ Génération de texte narratif
- ❌ Prompts

**Validation Phase 1 :**
- Un personnage peut s'entraîner hors-scène et progresser
- Un complot peut avancer sans être révélé
- Un événement caché peut être révélé plus tard
- Tout est reproductible et déterministe

---

### Phase 2 (Mémoire & Cohérence)

**Ajouts :**
- ✅ Résumés incluant les `HiddenEvents` (faits canoniques)
- ✅ Vérification de cohérence incluant les états cachés
- ✅ Détection de contradictions entre visible et caché

---

### Phase 3 (Orchestration)

**Ajouts :**
- ✅ Pipeline incluant la simulation hors-scène
- ✅ Orchestration des révélations
- ✅ Logging des événements cachés

---

### Phase 4-5 (LLM & Narration)

**Ajouts :**
- ✅ **HiddenNarrationAgent** : transforme les `HiddenEvent` en indices narratifs
- ✅ Génération de monologues internes
- ✅ Révélation partielle et progressive d'informations
- ✅ Création de suspense maîtrisé

**Important :** L'agent IA **ne crée pas** les événements, il **met en scène** ce qui existe déjà dans l'état caché.

---

## 🎯 Bénéfices

### Profondeur narrative
- Le monde vit même sans le héros
- Les personnages secondaires ont leur propre vie
- Cohérence temporelle renforcée

### Techniques narratives avancées
- **Dramatic irony** : Tension créée par la différence de connaissance
- **Twists** : Révélation d'événements passés cachés
- **Suspense** : Indices progressifs d'un danger imminent
- **Profondeur psychologique** : Pensées vs actions

### Qualité technique
- Déterminisme maintenu
- Testabilité complète
- Pas de dépendance aux prompts
- Traçabilité totale

---

## 📊 Exemple complet

### Scénario : Complot d'assassinat

```csharp
// Chapitre 1 : Création du complot (caché)
var plotCreationEvent = new HiddenEvent
{
    Id = Guid.NewGuid(),
    Type = EventType.PlotInitiated,
    Visibility = VisibilityLevel.Hidden,
    NarrativeTimestamp = chapter1Time,
    ActorIds = [villain1Id, villain2Id],
    Data = new Dictionary<string, object>
    {
        ["target"] = kingId,
        ["method"] = "poison",
        ["timeline_days"] = 60
    }
};

// Chapitre 5 : Progression du complot (indices)
var plotProgressEvent = new HiddenEvent
{
    Id = Guid.NewGuid(),
    Type = EventType.PlotProgression,
    Visibility = VisibilityLevel.Foreshadowed, // Indices donnés
    NarrativeTimestamp = chapter5Time,
    ActorIds = [villain1Id],
    Data = new Dictionary<string, object>
    {
        ["progress"] = 50,
        ["next_action"] = "acquire_poison"
    }
};

// Chapitre 10 : Le héros trouve une preuve
var revealResult = offSceneService.RevealHiddenEvent(
    hiddenEventId: plotCreationEvent.Id,
    method: RevealMethod.Document,
    revealerCharacterId: heroId
);

// Le lecteur découvre qu'un complot existait depuis le chapitre 1
// Dramatic irony inversé : le lecteur ne savait pas, découvre rétroactivement
```

---

## 🎓 Philosophie

> **"Le monde existe indépendamment de la caméra narrative."**

Cette approche garantit :
- Un univers cohérent et vivant
- Des révélations narratives puissantes
- Un déterminisme complet
- Une indépendance vis-à-vis de l'IA (Phase 1-3)

---

## 🔮 Évolution future

### Phase 5+ : Agents IA

Une fois le moteur stable, introduction possible de :

**HiddenNarrationAgent**
- Transforme les `HiddenEvent` en prose narrative
- Génère des indices subtils
- Crée des monologues internes
- Révèle partiellement les informations

**Règles strictes :**
- L'agent **ne crée pas** la réalité
- Il **révèle et met en scène** ce qui existe déjà
- Le moteur reste la source de vérité

---

## ✅ Checklist d'implémentation Phase 1

- [ ] Créer les entités : `HiddenEvent`, `InternalCharacterState`
- [ ] Créer les enums : `VisibilityLevel`, `RevealMethod`, `EmotionType`
- [ ] Implémenter `IOffSceneSimulationService`
- [ ] Ajouter les états cachés dans `StoryState`
- [ ] Créer les règles de progression hors-scène
- [ ] Implémenter le système de révélation
- [ ] Tests unitaires de simulation déterministe
- [ ] Tests de révélation d'événements
- [ ] Tests de cohérence visible/caché
- [ ] Documentation des patterns de révélation

---

## 📚 Références

- Document principal : [Phase1-Design.md](Phase1-Design.md)
- Architecture globale : [../ARCHITECTURE.md](../ARCHITECTURE.md)
- Roadmap : [ROADMAP.md](ROADMAP.md)
