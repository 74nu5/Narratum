# Roadmap - Narratum

## Vision à long terme

Narratum évoluera d'un moteur narratif déterministe vers un système complet de génération d'histoires interactives, en suivant une approche **anti-bidouille** par phases strictes.

## Principe directeur

> **Aucun LLM ne doit écrire une ligne tant que le moteur narratif n'est pas béton.**

Nous construisons **un moteur**, pas une démo.

---

## 🧱 PHASE 1 — FONDATIONS (SANS IA) ✅ EN COURS

### Objectif
Avoir un **moteur narratif testable sans IA**.

### Livrables
- ✅ Solution .NET 10 multi-projets
- ✅ Structure hexagonale (Core, Domain, State, Rules, Simulation, Persistence)
- ⏳ `Narratum.Core` COMPLET
- ⏳ États, univers, personnages, règles
- ⏳ Persistance SQLite
- ⏳ Tests unitaires

### Interdictions volontaires
- ❌ Appeler un LLM
- ❌ Générer du texte libre
- ❌ Faire une UI sexy

### Validation (checkpoint)
Vous devez pouvoir :
- Créer un univers
- Avancer une histoire
- Sauvegarder / charger
- Tout fonctionne **avec des textes mockés**

👉 Si vous vous ennuyez ici, c'est bon signe.

---

## 🧱 PHASE 2 — MÉMOIRE & COHÉRENCE (SANS CRÉATIVITÉ)

### Objectif
La continuité doit fonctionner **avant** l'écriture.

### Livrables
- `Narratum.Memory`
- Résumés hiérarchiques
- États canoniques
- Détection de contradictions (logique pure)

### Interdictions volontaires
- ❌ Générer de la belle prose
- ❌ Utiliser température > 0.3

### Validation
Vous devez pouvoir :
- Résumer 50 chapitres
- Retrouver un personnage
- Détecter une incohérence
- **Sans LLM**, ou avec LLM mocké ultra déterministe

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

## Statut actuel

📍 **PHASE 1 - Structure et fondations**

Prochaine étape : Implémenter les entités du Core (StoryWorld, Character, Event, etc.)
