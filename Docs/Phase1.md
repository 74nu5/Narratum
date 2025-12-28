# Phase 1 : Fondations (SANS IA)

## Principe directeur

> **Aucun LLM ne doit écrire une ligne tant que le moteur narratif n'est pas béton.**

Nous construisons **un moteur**, pas une démo.

---

## Documentation Phase 1

📘 **[Phase1-Design.md](Phase1-Design.md)** - Document d'architecture et de conception complet

Ce document contient :
- Architecture détaillée du moteur narratif
- Modèle de domaine complet (StoryWorld, Character, Event, etc.)
- Spécifications des services et règles
- Guide de développement étape par étape

---

## Objectif

Avoir un **moteur narratif testable sans IA**.

## Livrables Phase 1

### ✅ Étape 1.1 : Structure initiale (COMPLÉTÉ)

#### Structure de dossiers
- ✅ Core/
- ✅ Domain/
- ✅ State/
- ✅ Rules/
- ✅ Simulation/
- ✅ Persistence/
- ✅ Tests/
- ✅ Docs/

#### Documentation
- ✅ README.md (racine)
- ✅ ARCHITECTURE.md
- ✅ Phase1.md (ce fichier)
- ✅ ROADMAP.md
- ✅ CONTRIBUTING.md
- ✅ README.md dans chaque dossier

#### Configuration .NET
- ✅ Directory.Build.props
- ✅ .gitignore

### ⏳ Étape 1.2 : Core & Domain (À FAIRE)

- ⏳ Entités principales :
  - StoryWorld
  - StoryArc
  - StoryChapter
  - StoryState
  - Character
  - Location
  - Event
- ⏳ Value Objects
- ⏳ Invariants du domaine
- ⏳ Interfaces (ports)

### ⏳ Étape 1.3 : State Management (À FAIRE)

- ⏳ Représentation immuable de l'état
- ⏳ Transitions d'état déterministes
- ⏳ Snapshots
- ⏳ Historique des changements

### ⏳ Étape 1.4 : Rules Engine (À FAIRE)

- ⏳ Moteur d'évaluation des règles
- ⏳ Règles narratives de base
- ⏳ Conditions et effets
- ⏳ Validation déterministe

### ⏳ Étape 1.5 : Persistence (À FAIRE)

- ⏳ Sérialisation/désérialisation
- ⏳ SQLite integration
- ⏳ Sauvegarde/chargement d'états
- ⏳ Migrations

### ⏳ Étape 1.6 : Tests unitaires (À FAIRE)

- ⏳ Tests du Core
- ⏳ Tests du Domain
- ⏳ Tests du State
- ⏳ Tests des Rules
- ⏳ Tests de Persistence

---

## Interdictions volontaires de la Phase 1

- ❌ **Appeler un LLM** - Aucune dépendance IA
- ❌ **Générer du texte libre** - Textes mockés uniquement
- ❌ **Faire une UI** - Core library uniquement

👉 Si vous vous ennuyez ici, c'est bon signe.

---

## Validation complète de la Phase 1

La Phase 1 sera considérée comme terminée quand vous pourrez :

1. ✅ Créer un univers (StoryWorld)
2. ✅ Définir des personnages (Character)
3. ✅ Créer un arc narratif (StoryArc)
4. ✅ Avancer l'histoire chapitre par chapitre
5. ✅ Évaluer des règles narratives
6. ✅ Sauvegarder l'état complet
7. ✅ Charger un état sauvegardé
8. ✅ Reproduire exactement la même séquence d'événements

**Tout doit fonctionner avec des textes mockés/prédéfinis.**

---

## Prochaines phases

Consultez [ROADMAP.md](ROADMAP.md) pour le plan complet :

- **Phase 2** : Mémoire & Cohérence (sans créativité)
- **Phase 3** : Orchestration (LLM en boîte noire)
- **Phase 4** : Intégration LLM minimale
- **Phase 5** : Narration contrôlée
- **Phase 6** : UI et expérience utilisateur

---

## Pourquoi cette approche ?

Cette stratégie **anti-bidouille** garantit :

✔️ Architecture propre et maintenable
✔️ Pas de dette technique
✔️ Testabilité complète
✔️ Déterminisme garanti
✔️ Projet qui va au bout

> **"Retarder volontairement le plaisir du résultat visible"** pour construire quelque chose qui dure.
