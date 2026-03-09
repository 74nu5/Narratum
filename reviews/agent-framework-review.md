# Revue de l'Architecture Agent — Narratum

**Date :** 2026-03-09
**Portée :** Modules Orchestration, Llm, Web — analyse de l'architecture agent et de l'intégration LLM par rapport à Microsoft Agent Framework (.NET).

---

## 1. Résumé Exécutif

Narratum implémente un système multi-agent artisanal (custom-built) pour la génération narrative, avec un pipeline en 5 étapes (ContextBuilder → PromptBuilder → AgentExecutor → OutputValidator → StateIntegrator). Cette architecture est fonctionnelle et bien testée (894 tests), mais elle ne s'appuie **pas** sur Microsoft Agent Framework (Microsoft.Agents.AI). Elle utilise directement Microsoft.Extensions.AI (v10.2.0) comme couche d'abstraction LLM, avec Foundry Local et Ollama comme fournisseurs.

### Verdict Global

| Critère | Évaluation |
|---|---|
| **Architecture agent** | 🟡 Artisanale, solide mais non standard |
| **Intégration LLM** | 🟢 Bonne via Microsoft.Extensions.AI |
| **Pipeline d'orchestration** | 🟢 Bien conçu, déterministe, avec retry |
| **Alignement Microsoft Agent Framework** | 🔴 Aucun — framework non utilisé |
| **Testabilité** | 🟢 Excellente — mocks partout, 894 tests |
| **Extensibilité** | 🟡 Possible mais couplage interne |

---

## 2. Architecture Agent Actuelle

### 2.1 Hiérarchie des Agents

`
IAgent (interface de base)
├── INarratorAgent    → Génération de prose narrative
├── ICharacterAgent   → Dialogues et réactions de personnages
├── ISummaryAgent     → Résumés d'événements et chapitres
└── IConsistencyAgent → Vérification de cohérence narrative
`

Chaque agent a une implémentation mock (MockNarratorAgent, etc.) permettant de valider l'architecture sans LLM réel. C'est un **excellent pattern** qui respecte le principe de Narratum : « le système fonctionne même avec un LLM stupide ».

### 2.2 Pipeline d'Orchestration

Le pipeline est implémenté dans deux services :

- **OrchestrationService** — version simplifiée (Phase 3) avec 5 étapes inline
- **FullOrchestrationService** — version complète avec validation structurelle/cohérence, retry, audit trail, métriques

`
StoryState + NarrativeIntent
    │
    ▼
ContextBuilder ────► NarrativeContext (personnages actifs, lieu, mémoire, faits)
    │
    ▼
PromptBuilder ─────► PromptSet (system + user prompts par agent)
    │
    ▼
AgentExecutor ─────► RawOutput (réponses par AgentType)
    │
    ▼
OutputValidator ───► ValidationResult (structurel + cohérence + logique narrative)
    │
    ▼
StateIntegrator ───► NarrativeOutput (texte combiné + events générés + state changes)
`

### 2.3 Intégration LLM

`
ILlmClient (Orchestration — abstraction pure)
    │
    ├── MockLlmClient ──► Réponses déterministes pour tests
    │
    └── ChatClientLlmAdapter (Llm) ──► IChatClient (Microsoft.Extensions.AI)
            │
            ├── OpenAI SDK → Foundry Local (endpoint local)
            └── OllamaSharp → Ollama
`

La chaîne de création est : LlmClientFactory → IChatClient → ChatClientLlmAdapter → ILlmClient.
Un LazyLlmClient wrapper évite le blocage au démarrage.

---

## 3. Analyse Comparative avec Microsoft Agent Framework

### 3.1 Ce que Microsoft Agent Framework apporte

Microsoft Agent Framework (Microsoft.Agents.AI, en preview publique) unifie Semantic Kernel et AutoGen. Ses concepts clés :

| Concept | Description |
|---|---|
| **AI Agents** | Agents autonomes avec tools, MCP servers, middleware, context providers |
| **Workflows** | Orchestration graph-based avec edges conditionnels, checkpointing |
| **Thread State** | Gestion d'état conversationnel intégrée |
| **Tool Calling** | Agents qui invoquent des fonctions typées |
| **Multi-Agent Patterns** | Sequential, concurrent, hand-off, Magentic-One |
| **Middleware** | Interception/enrichissement des actions d'agent |

### 3.2 Mapping Narratum ↔ Agent Framework

| Narratum (actuel) | Agent Framework (équivalent) | Écart |
|---|---|---|
| IAgent interface | Agent base class | Narratum réinvente une hiérarchie d'agents |
| AgentExecutor | Agent runtime / workflow executor | L'exécution est manuelle vs déclarative |
| NarrativeContext | Thread state + context providers | Narratum construit le contexte à la main |
| PromptBuilder / IPromptTemplate | Prompt templates / structured outputs | Narratum a un bon système de templates |
| ILlmClient → IChatClient | Model providers (Azure AI Foundry, etc.) | Bonne abstraction via Microsoft.Extensions.AI |
| OutputValidator | Middleware / output validation | Pas d'équivalent direct dans AF — Narratum a un avantage ici |
| RetryHandler + policies | Built-in retry dans le runtime | Narratum a des policies plus riches |
| AuditTrail / PipelineLogger | Tracing / OpenTelemetry | AF utilise OTel ; Narratum a son propre système |
| AgentType enum routing | Agent routing / hand-off | AF a un routage plus flexible |
| PromptSet (Sequential/Parallel/Conditional) | Workflow edges (graph-based) | AF est plus expressif |

---

## 4. Points Forts de l'Architecture Actuelle

### ✅ 4.1 Séparation claire Orchestration / LLM

Le module Orchestration ne dépend d'aucune librairie LLM. ILlmClient est une interface pure définie dans Orchestration.Llm. Le module Llm implémente cette interface via ChatClientLlmAdapter. C'est une **excellente application de l'architecture hexagonale**.

### ✅ 4.2 Déterminisme garanti

Le pipeline est déterministe (hors LLM) : même NarrativeContext + même NarrativeIntent → même PromptSet. Les mock agents produisent des sorties déterministes, ce qui permet des tests reproductibles.

### ✅ 4.3 Validation multi-niveaux

OutputValidator applique 4 niveaux de validation :
1. **Structurelle** — contenu non vide, longueur minimale
2. **Contenu** — patterns interdits, longueur max
3. **Cohérence** — intégration avec ICoherenceValidator (Phase 2 Memory)
4. **Logique narrative** — personnages morts ne parlent pas, lieu mentionné

C'est un point fort que Microsoft Agent Framework ne fournit pas nativement.

### ✅ 4.4 Retry sophistiqué

4 politiques de retry (SimpleRetryPolicy, ExponentialBackoffRetryPolicy, ConditionalRetryPolicy, NoRetryPolicy) avec un RetryHandler générique. C'est plus riche que ce qu'offre AF par défaut.

### ✅ 4.5 Routing de modèle par agent

LlmClientConfig.ResolveModel(AgentType) permet d'assigner un modèle LLM différent à chaque type d'agent (Narrator peut utiliser un modèle plus puissant que Summary). La priorité NarratorModel > AgentModelMapping > DefaultModel est bien pensée.

### ✅ 4.6 Testabilité exemplaire

- MockLlmClient pour les tests sans LLM
- MockNarratorAgent, MockCharacterAgent, etc.
- LazyLlmClient pour éviter l'init LLM au démarrage
- 894 tests passants

---

## 5. Points d'Attention et Risques

### 🟡 5.1 Architecture Artisanale vs Standard

**Constat :** Narratum réinvente plusieurs concepts que Microsoft Agent Framework fournit nativement (agents, orchestration, state management, model providers).

**Risque :** Maintenance à long terme. Si le projet évolue vers des scénarios plus complexes (agents conversationnels, tool calling, MCP servers), il faudra soit :
- Migrer vers AF (coûteux)
- Continuer à réinventer (coûteux aussi)

**Évaluation :** Ce n'est pas un problème pour le cas d'usage actuel (pipeline déterministe de génération narrative). AF est optimisé pour les agents conversationnels autonomes, ce qui n'est **pas** le paradigme de Narratum. Le choix artisanal est **défendable** tant que le LLM reste un « moteur de génération » passif.

### 🟡 5.2 Deux Services d'Orchestration

**Constat :** OrchestrationService et FullOrchestrationService coexistent avec des logiques dupliquées. Le Web utilise uniquement FullOrchestrationService.

**Risque :** Divergence de comportement, confusion sur lequel utiliser.

**Recommandation :** Considérer la dépréciation d'OrchestrationService au profit de FullOrchestrationService, ou refactorer pour que OrchestrationService délègue à FullOrchestrationService avec une config simplifiée.

### 🟡 5.3 Agents Non Connectés au Pipeline Principal

**Constat :** Les interfaces spécialisées (INarratorAgent.GenerateNarrativeAsync, ICharacterAgent.GenerateDialogueAsync, etc.) ne sont **pas utilisées** par FullOrchestrationService. Celui-ci appelle directement ILlmClient.GenerateAsync avec des prompts construits par PromptRegistry.

Les mock agents existent et implémentent IAgent.ProcessAsync et les méthodes spécialisées, mais le pipeline principal bypass complètement la hiérarchie d'agents.

**Risque :** Les interfaces agent sont « dead code » dans le flux réel. Elles sont testées isolément mais jamais appelées en production.

**Recommandation :** Soit intégrer les agents typés dans FullOrchestrationService (chaque AgentType → appel au bon IAgent.ProcessAsync), soit simplifier en supprimant la couche IAgent et en gardant uniquement le pattern prompt template + LLM client.

### 🟡 5.4 Absence de Tool Calling / Function Calling

**Constat :** Les agents ne peuvent pas invoquer de fonctions/outils. Ils reçoivent un prompt et retournent du texte brut.

**Impact :** C'est **cohérent avec la philosophie Narratum** (le LLM est une boîte noire de génération). Mais cela limite l'évolutivité vers des agents plus autonomes (ex: un agent qui consulte l'état du monde ou modifie des faits).

### 🟡 5.5 DateTime.UtcNow dans le Code Métier

**Constat :** Plusieurs classes utilisent directement DateTime.UtcNow :
- NarrativeContext.ContextBuiltAt dans le constructeur
- RawOutput.Create → DateTime.UtcNow
- LlmRequest.CreatedAt dans le constructeur
- LlmResponse.CreatedAt dans le constructeur
- FullPipelineResult → CompletedAt
- AuditEntry.Create → DateTime.UtcNow
- GeneratedEvent.Create → DateTime.UtcNow

**Risque :** Viole la règle de déterminisme du projet (« pas de DateTime.UtcNow direct dans la logique métier »). Rend les tests sensibles au timing.

**Recommandation :** Injecter un TimeProvider (ou IClock) dans les services et records qui ont besoin d'un timestamp.

### 🔴 5.6 IsMock Couplage dans ILlmClient

**Constat :** ILlmClient expose une propriété IsMock qui est utilisée dans les métadonnées de NarrativeOutput. Cela couple l'interface d'abstraction à un détail d'implémentation (est-ce un mock ou pas ?).

**Recommandation :** Supprimer IsMock de ILlmClient. Si nécessaire, exposer cette information via les métadonnées de LlmResponse ou via un service de diagnostic séparé.

---

## 6. Recommandations

### 6.1 Court Terme (Quick Wins)

| # | Action | Effort | Impact |
|---|---|---|---|
| 1 | Remplacer DateTime.UtcNow par TimeProvider injecté | Moyen | Élevé (déterminisme) |
| 2 | Supprimer IsMock de ILlmClient | Faible | Moyen (propreté API) |
| 3 | Clarifier le rôle d'OrchestrationService vs FullOrchestrationService | Faible | Moyen (maintenabilité) |

### 6.2 Moyen Terme (Refactoring)

| # | Action | Effort | Impact |
|---|---|---|---|
| 4 | Connecter les IAgent typés au pipeline ou les supprimer | Moyen | Élevé (dead code) |
| 5 | Extraire les AgentType metadata keys en constantes partagées | Faible | Faible (clarté) |
| 6 | Ajouter OpenTelemetry pour le tracing (remplacer PipelineLogger custom) | Moyen | Moyen (observabilité) |

### 6.3 Long Terme (Évolution Architecturale)

| # | Action | Effort | Impact |
|---|---|---|---|
| 7 | Évaluer Microsoft Agent Framework si besoin d'agents autonomes/conversationnels | Élevé | Élevé |
| 8 | Implémenter du tool calling si les agents doivent consulter l'état du monde | Élevé | Élevé |
| 9 | Passer à un workflow graph-based si les scénarios d'orchestration se complexifient | Élevé | Élevé |

---

## 7. Faut-il Migrer vers Microsoft Agent Framework ?

### Arguments Pour

- Standard Microsoft, communauté active, documentation officielle
- Multi-agent patterns intégrés (sequential, concurrent, hand-off)
- Thread state management intégré
- Tool calling / MCP servers natifs
- OpenTelemetry tracing intégré
- Évolutivité vers des scénarios conversationnels

### Arguments Contre

- **Narratum n'est pas un système conversationnel** — le LLM est un moteur de génération passif, pas un agent autonome
- **Le pipeline déterministe est un avantage** — AF est conçu pour des agents qui prennent des décisions, pas pour du content generation contrôlé
- **Coût de migration élevé** — 894 tests à adapter, architecture hexagonale à préserver
- **AF est en preview publique** — API instable, breaking changes possibles
- **L'architecture actuelle fonctionne** — pas de douleur justifiant une migration

### Recommandation

**Ne pas migrer pour le moment.** L'architecture actuelle est bien adaptée au cas d'usage de Narratum (génération narrative déterministe avec LLM passif). La migration vers AF ne se justifierait que si :
1. Narratum évolue vers des agents autonomes (LLM qui prend des décisions)
2. Le tool calling devient nécessaire (agents qui consultent/modifient l'état)
3. AF atteint la GA (General Availability) avec une API stable

En attendant, continuer à utiliser Microsoft.Extensions.AI comme couche d'abstraction LLM — c'est la même couche que AF utilise en interne.

---

## 8. Métriques d'Architecture

| Métrique | Valeur |
|---|---|
| Fichiers C# dans Orchestration | 42 |
| Fichiers C# dans Llm | 9 |
| Interfaces d'agent | 5 (IAgent, INarratorAgent, ICharacterAgent, ISummaryAgent, IConsistencyAgent) |
| Implémentations mock | 4 |
| Étapes du pipeline | 5 |
| Politiques de retry | 4 |
| Packages NuGet LLM | 4 (Microsoft.Extensions.AI, Microsoft.AI.Foundry.Local, OllamaSharp, OpenAI) |
| Tests | 894 |

---

*Revue effectuée par analyse statique du code source. Les recommandations concernant Microsoft Agent Framework sont basées sur la documentation publique du framework (preview publique, mars 2026).*
