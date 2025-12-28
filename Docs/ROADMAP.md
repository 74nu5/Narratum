# Roadmap - Narratum

## Vision à long terme

Narratum évoluera d'un moteur narratif déterministe vers un système complet de génération d'histoires interactives, en suivant une approche **anti-bidouille** par phases strictes.

## Principe directeur

> **Aucun LLM ne doit écrire une ligne tant que le moteur narratif n'est pas béton.**

Nous construisons **un moteur**, pas une démo.

---

## 🧱 PHASE 1 — FONDATIONS (SANS IA) ✅ COMPLÉTÉE

### Objectif
Avoir un **moteur narratif testable sans IA**. ✅ ATTEINT

### Livrables
- ✅ Solution .NET 10 multi-projets (7 modules)
- ✅ Structure hexagonale (Core, Domain, State, Rules, Simulation, Persistence)
- ✅ `Narratum.Core` complet avec Result<T>, Id, Unit, Enums
- ✅ Entités complètes : StoryWorld, Character, Location, Event, StoryArc, StoryChapter
- ✅ Gestion d'état immuable : WorldState, CharacterState, StoryState
- ✅ Moteur de règles : RuleEngine avec validation
- ✅ Persistance SQLite : Snapshots déterministes + EF Core
- ✅ Tests unitaires : 110/110 passants (65 Phase 1.6 + 45 baseline)
- ✅ Application Playground : Démo narrative complète avec Spectre.Console

### Résultats Finaux
- **Build**: 0 erreurs, 0 warnings
- **Tests**: 110/110 passing (100%)
- **Code**: ~3000+ lignes, architecture hexagonale clean
- **Démo**: Histoire de 3 chapitres sur 10 heures, mort de personnage, snapshots

### Interdictions Respectées
- ✅ Aucun appel à un LLM
- ✅ Aucune génération de texte libre (mockée seulement)
- ✅ Aucune UI (Playground est CLI)

### Validation Complète ✅
- ✅ Créer un univers (The Hidden Realm)
- ✅ Avancer une histoire (3 chapitres, 10 heures)
- ✅ Ajouter des personnages (3 personnes avec traits)
- ✅ Créer des événements (mouvements, morts, révélations)
- ✅ Sauvegarder et charger (snapshots)
- ✅ Valider les règles (RuleEngine)
- ✅ Tout fonctionne **avec des textes mockés**

👉 **Phase 1 = Fondations Solides ✅**

---

## 🧱 PHASE 2 — MÉMOIRE & COHÉRENCE (SANS CRÉATIVITÉ) 📋 DESIGN COMPLET

### Objectif
La continuité doit fonctionner **avant** l'écriture. Les résumés et la cohérence doivent être **déterministes et sans LLM**.

### Livrables Planifiés
- 📚 `Narratum.Memory` (nouveau module)
- 📚 IFactExtractor - Extraction de faits depuis les événements
- 📚 ISummaryGenerator - Résumés hiérarchiques (4 niveaux)
- 📚 CanonicalState - État "ground truth" d'une histoire
- 🧠 ICoherenceValidator - Détection de contradictions
- 💾 IMemoryRepository - Persistance des memorias (SQLite)
- ✅ Tests unitaires et d'intégration

### Architecture Détaillée
📖 **[Phase2-Design.md](Phase2-Design.md)** - Document complet (180+ lignes) avec:
- **Vue d'ensemble**: Architecture globale, intégration, flux de données
- **Modules**: Models, Services, Layers, Coherence, Store
- **Algorithmes**: Extraction, résumé hiérarchique, détection de contradictions
- **APIs**: IMemoryService, interfaces complètes, exemples d'utilisation
- **Plan**: 8 étapes de développement avec checkpoints
- **Tests**: Unitaires, intégration, cas réels, performance
- **Interdictions**: Pas de LLM, pas de stochastique, pas de texte libre

### 4 Niveaux de Mémoire
1. **Level 0 - Event**: Un seul événement → Faits élémentaires
2. **Level 1 - Chapter**: Groupe d'événements → Résumé scène
3. **Level 2 - Arc**: Groupe de chapitres → Résumé narratif
4. **Level 3 - World**: Histoire complète → Résumé global

### Interdictions Volontaires
- ❌ Aucun appel LLM (Phase 2 = logique pure)
- ❌ Aucune génération de texte libre
- ❌ Aucune randomisation ou température
- ❌ Aucune modification du Core/Phase 1
- ❌ Aucun cache non-invalidable

### Validation Prévue
- ✅ Résumer 50+ chapitres (déterministe)
- ✅ Retrouver un personnage dans l'historique
- ✅ Détecter une incohérence (mort → vivant)
- ✅ Extraire tous les changements d'état
- ✅ Performance: 100 events en < 500ms

👉 **Phase 2 = Memory + Coherence (Logique Pure)**

---

## 🧱 PHASE 3 — ORCHESTRATION (LLM EN BOÎTE NOIRE)

### Objectif
Le système fonctionne **même si le LLM est stupide**.

### Livrables
- `Narratum.Orchestration`
- Pipeline complet
- Agents simulés
- Rewriting contrôlé
- Logging exhaustif

### Interdictions volontaires
- ❌ Changer la logique métier à cause d'un LLM
- ❌ Laisser un agent décider seul

### Validation
Vous pouvez remplacer le LLM par :
```csharp
return "TEXTE FAUX MAIS STRUCTURELLEMENT VALIDE";
```

👉 Si ça marche, vous êtes prêt.

---

## 🧱 PHASE 4 — INTÉGRATION LLM MINIMALE

### Objectif
Brancher l'IA **sans casser l'architecture**.

### Livrables
- `Narratum.LLM` (abstraction)
- `ILlmClient`
- llama.cpp ou Ollama
- Un seul agent actif : **SummaryAgent**

### Interdictions volontaires
- ❌ Faire écrire l'histoire
- ❌ Toucher au Core
- ❌ Modifier l'orchestrateur

### Validation
- Les résumés sont meilleurs qu'avant
- Le reste du système est inchangé

---

## 🧱 PHASE 5 — NARRATION CONTRÔLÉE

### Objectif
Enfin… écrire.

### Livrables
- NarratorAgent
- Température maîtrisée
- Prompt strict
- LoRA narratif
- CharacterAgent
- ConsistencyAgent

### Interdictions volontaires
- ❌ Mettre toute la logique dans le prompt
- ❌ Ignorer les agents de contrôle

### Validation
- Texte beau
- Cohérence maintenue sur 20+ itérations
- Zéro régression métier

---

## 🧱 PHASE 6 — UI ET EXPÉRIENCE UTILISATEUR

### Objectif
Rendre le système accessible.

### Livrables
- `Narratum.UI` (Blazor WebView / MAUI / Avalonia)
- `Narratum.Api` (ASP.NET Core)
- Interface immersive
- Fiches personnages
- Timeline
- Sauvegardes

---

## 🧠 RÈGLES PSYCHOLOGIQUES

### 1️⃣ Pas d'UI avant la phase 6
👉 UI = dopamine = abandon prématuré

### 2️⃣ Tests > démo
- Test vert = progression
- Démo = bonus

### 3️⃣ Tout ce qui est flou est interdit
- prompt vague ❌
- état implicite ❌
- magie ❌

---

## 📐 OUTILS ANTI-DÉVIATION

- Tests unitaires obligatoires
- Logs narratifs lisibles
- Mode "LLM OFF"
- Feature flags
- Documentation d'architecture à jour

---

## 🎯 BÉNÉFICES DE CETTE APPROCHE

✔️ Architecture propre
✔️ Pas de dette technique
✔️ Pas de "j'ai peur de toucher"
✔️ Possibilité d'arrêter/reprendre quand vous voulez
✔️ Projet qui va **au bout**

---

## Architecture cible (Phase 5+)

Lorsque toutes les phases seront complètes, Narratum ressemblera à :

```
UI → API .NET → Orchestrateur → Agents IA → Mémoire → Persistance
```

### Modules finaux
- `Narratum.UI` - Interface utilisateur
- `Narratum.Api` - API REST
- `Narratum.Core` - Domaine narratif (pur, sans IA)
- `Narratum.Orchestration` - Pipeline & agents
- `Narratum.LLM` - Abstraction LLM local
- `Narratum.Memory` - Mémoire, résumés, contexte
- `Narratum.Persistence` - SQLite / LiteDB
- `Narratum.Shared` - DTO, contrats

### Agents IA (Phase 5)
1. **NarratorAgent** - Génération du texte principal
2. **CharacterAgent** - Dialogues et réactions des personnages
3. **SummaryAgent** - Résumés factuels et compression
4. **ConsistencyAgent** - Vérification de cohérence

### Configuration matérielle cible
- CPU haut de gamme
- 128 Go RAM
- GPU AMD RX 6950 XT (16 Go VRAM)
- 100% local, aucun cloud

---

## Statut Actuel

✅ **PHASE 1 - COMPLÉTÉE** 🎉
- 110/110 tests passants
- 7 modules compilés (0 erreurs)
- Démo Playground narrative fonctionnelle
- Architecture hexagonale validée
- Documentation complète

📋 **PHASE 2 - Design Complet, Prêt à Développer**
- Architecture documentée dans Phase2-Design.md
- Tous les composants spécifiés avec code exemple
- Plan de développement en 8 étapes
- APIs publiques définies
- Critères de validation clairs

📅 **Prochaine Étape**: Étape 2.1 — Créer Narratum.Memory.Models
