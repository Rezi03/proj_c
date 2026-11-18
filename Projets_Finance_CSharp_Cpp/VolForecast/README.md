# Prévision de la volatilité du CAC 40 (C# .NET 10 et C++)

Le programme compare cinq modèles de prévision de la volatilité sur cinq ans de données jamais vues pendant l'estimation. Il donne la prévision pour demain et pour les six prochains mois, puis produit un **rapport PDF** de 7 pages.

## Ce que fait le programme

1. **Données** : il télécharge le CAC 40 depuis 2005 (ouverture, plus haut, plus bas, clôture) sur Yahoo Finance. Les cours sont mis en cache dans `data/`.
2. **Volatilité réalisée** : il la mesure chaque jour avec l'estimateur de Garman-Klass, plus le saut de la nuit.
3. **Cinq modèles** : volatilité historique, EWMA RiskMetrics, GARCH(1,1), GJR-GARCH(1,1) (effet de levier) et HAR-RV de Corsi.
4. **Estimation** : par maximum de vraisemblance. La vraisemblance est calculée en C++ et l'optimiseur Nelder-Mead est écrit en C#.
5. **Test hors échantillon** : les prévisions sont glissantes et les modèles sont ré-estimés chaque mois sans jamais voir le futur.
6. **Évaluation** : perte QLIKE, RMSE, régression de Mincer-Zarnowitz, test de Diebold-Mariano contre la référence.
7. **Application** : il calcule la VaR 99 % issue de chaque modèle et la backteste avec le test de Kupiec.
8. **Prévisions** : volatilité de demain, courbe sur six mois, courbe d'impact des nouvelles.

## Lancer le projet sur Mac

```bash
# Une seule fois
brew install --cask dotnet-sdk      # SDK .NET 10
xcode-select --install              # compilateur C++ (clang)

# Puis à chaque fois
chmod +x build.sh
./build.sh
```

Le PDF est créé dans `output/` et s'ouvre automatiquement.
Si le moteur C++ n'est pas compilé, le programme passe automatiquement sur une version C# du même calcul.

## Personnaliser

Les paramètres sont dans `src/Config.cs`. Vous pouvez changer l'indice (par exemple `^GSPC` pour le S&P 500 ou `^STOXX50E` pour l'Euro Stoxx 50), la période de test et la fréquence de ré-estimation.

## Structure

```
native/volengine.cpp         Vraisemblance et filtre GARCH / GJR-GARCH en C++17
src/Config.cs                Paramètres de l'étude
src/Data/MarketData.cs       Téléchargement Yahoo Finance et cache CSV
src/Numerics/Stats.cs        Statistiques et algèbre linéaire (fait main)
src/Models/                  Modèles, optimiseur, prévisions glissantes, évaluation
src/Report/                  Graphiques SVG et rapport PDF
```
