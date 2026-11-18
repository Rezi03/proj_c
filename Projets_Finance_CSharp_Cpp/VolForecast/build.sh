#!/usr/bin/env bash
# Compile le moteur C++, puis lance le programme C# qui produit le rapport PDF.
# Usage : ./build.sh
set -euo pipefail
cd "$(dirname "$0")"

case "$(uname -s)" in
  Darwin) EXT="dylib" ;;
  Linux)  EXT="so" ;;
  *)      EXT="dll" ;;
esac

echo "→ Compilation du moteur C++ (libvolengine.$EXT)"
mkdir -p native/lib
c++ -O3 -std=c++17 -shared -fPIC native/volengine.cpp -o "native/lib/libvolengine.$EXT"

echo "→ Lancement du programme C# (.NET)"
dotnet run -c Release --project src/VolForecast.csproj
