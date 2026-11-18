#!/usr/bin/env bash
# Met à jour les deux analyses avec les dernières données de marché et régénère les deux PDF.
# Usage : ./actualiser.sh
set -euo pipefail
cd "$(dirname "$0")"
echo "════ Projet 1 : moteur de risque ════"
(cd RiskEngine && ./build.sh)
echo
echo "════ Projet 2 : prévision de volatilité ════"
(cd VolForecast && ./build.sh)
