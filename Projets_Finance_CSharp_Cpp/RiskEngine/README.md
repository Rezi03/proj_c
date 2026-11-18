# Moteur de risque de marché (C# .NET 10 et C++)

Le programme calcule le risque d'un portefeuille de 10 M€ investi dans 8 grandes actions françaises, puis produit un **rapport PDF** de 8 pages.

## Ce que fait le programme

1. **Données** : il télécharge l'historique depuis 2007 et la dernière séance sur Yahoo Finance. Les cours sont mis en cache dans `data/`.
2. **VaR 99 % et Expected Shortfall 97,5 %** : il les calcule selon cinq méthodes (historique, paramétrique, EWMA RiskMetrics, Monte Carlo normale, Monte Carlo Student-t).
3. **Monte Carlo en C++** : il simule 200 000 scénarios corrélés (Cholesky) en multi-thread, appelés depuis C# par P/Invoke.
4. **Contributions au risque** : il décompose la VaR par action (méthode d'Euler) et calcule les corrélations, les bêtas et le gain de diversification.
5. **Backtesting** : il compte les exceptions sur 250 et 750 jours, applique les feux tricolores de Bâle et les tests de Kupiec et de Christoffersen.
6. **Stress tests** : il rejoue cinq crises réelles (Lehman, dette souveraine, Covid, Ukraine, dissolution de 2024) et quatre chocs hypothétiques.
7. **Rapport PDF** : la mise en page est faite avec QuestPDF et les graphiques vectoriels en SVG sont faits maison.

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

Tous les paramètres sont dans `src/Config.cs` : actions, poids, montant, niveaux de confiance, fenêtres, scénarios de stress.

## Structure

```
native/riskengine.cpp        Moteur Monte Carlo C++17 multi-thread
src/Config.cs                Portefeuille et paramètres
src/Data/MarketData.cs       Téléchargement Yahoo Finance et cache CSV
src/Numerics/Stats.cs        Statistiques et algèbre linéaire (fait main)
src/Analytics/               VaR, ES, Monte Carlo, backtesting, stress tests
src/Report/                  Graphiques SVG et rapport PDF
```
