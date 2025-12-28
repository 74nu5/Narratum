# Simulation

Ce dossier orchestre l'exécution de la simulation narrative.

## Responsabilités

- Coordonner l'exécution des règles et des transitions d'état
- Gérer le déroulement temporel de la simulation
- Fournir les services d'orchestration du moteur narratif
- **Simuler les événements hors-scène et cachés**

## Services principaux (Phase 1)

### StoryProgressionService
Service orchestrant la progression narrative.

**Flux de traitement :**
1. Recevoir une `StoryAction`
2. Valider via les `IStoryRule`
3. Appliquer les transformations d'état
4. Générer les `Event` résultants
5. Mettre à jour le `StoryState`
6. Retourner le résultat

### StateTransitionService
Service gérant les transitions d'état.

**Responsabilités :**
- Appliquer les actions sur l'état
- Générer les événements
- Maintenir l'intégrité de l'état

### OffSceneSimulationService
Service de simulation hors-scène (événements cachés).

**Responsabilités :**
- Simuler les événements du monde hors caméra
- Gérer l'évolution des personnages hors scène
- Progresser les intentions et plans cachés
- Révéler les événements cachés selon les méthodes narratives

**Flux :**
- S'exécute entre les chapitres
- Produit des `HiddenEvent` avec niveaux de visibilité
- Respecte le déterminisme complet
- Ne génère jamais de texte (Phase 1)

👉 Documentation complète : [../Docs/HiddenWorldSimulation.md](../Docs/HiddenWorldSimulation.md)

## StoryAction

Action utilisateur ou système.

**Types d'actions :**
- Avancer le temps narratif
- Déplacer un personnage
- Déclencher un événement
- Terminer un chapitre
- Créer une relation entre personnages
- Révéler un événement caché

## Principes

- Exécution déterministe
- Gestion du temps simulé (narratif, pas réel)
- Coordination des différents modules
- Validation avant toute action
- Génération d'événements traçables
- **Le monde vit même hors de la vue du protagoniste**
