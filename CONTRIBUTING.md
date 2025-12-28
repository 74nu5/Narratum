# Contribuer à Narratum

Merci de votre intérêt pour Narratum ! Ce document explique comment contribuer au projet.

## Principes de contribution

### 1. Déterminisme avant tout

Toutes les contributions doivent respecter le principe de déterminisme :

- Pas de génération aléatoire non-seedée
- Pas d'accès à l'horloge système non contrôlé
- Pas de dépendances sur des services externes non déterministes

### 2. Architecture hexagonale

Respectez la structure en couches :

- La logique métier doit rester dans Domain
- Les abstractions vont dans Core
- Les implémentations concrètes vont dans les adaptateurs

### 3. Pas de dépendances inutiles

- Pas de frameworks LLM ou IA générative
- Pas de frameworks UI
- Minimiser les dépendances externes
- Utiliser les bibliothèques standard .NET autant que possible

## Processus de contribution

### 1. Fork et branche

1. Forkez le repository
2. Créez une branche descriptive : `feature/nom-de-la-fonctionnalite`
3. Travaillez sur votre branche

### 2. Conventions de code

- Suivez les conventions C# standard
- Utilisez des noms descriptifs
- Commentez le code complexe
- Documentez les API publiques avec XML comments

### 3. Tests

Toute contribution doit inclure :

- Tests unitaires pour la nouvelle fonctionnalité
- Tests de déterminisme si applicable
- Tous les tests existants doivent passer

### 4. Documentation

- Mettez à jour la documentation si nécessaire
- Ajoutez des exemples pour les nouvelles fonctionnalités
- Expliquez les choix architecturaux non évidents

### 5. Pull Request

1. Assurez-vous que tous les tests passent
2. Vérifiez que le build réussit
3. Créez une Pull Request avec :
   - Titre descriptif
   - Description des changements
   - Références aux issues concernées
   - Captures d'écran si applicable

## Structure des commits

Format recommandé :

``` txt
<type>: <description courte>

<description détaillée si nécessaire>

Fixes #<numéro-issue>
```

Types :

- `feat`: Nouvelle fonctionnalité
- `fix`: Correction de bug
- `docs`: Documentation uniquement
- `refactor`: Refactoring sans changement de fonctionnalité
- `test`: Ajout ou modification de tests
- `chore`: Tâches de maintenance

## Questions ?

- Ouvrez une issue pour les questions générales
- Commentez sur une issue existante pour des clarifications

## Code de conduite

- Soyez respectueux et constructif
- Acceptez les critiques constructives
- Concentrez-vous sur ce qui est le mieux pour le projet

Merci de contribuer à Narratum !
