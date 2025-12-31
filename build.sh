#!/bin/bash
# KernelDash Build & Deploy Script

echo "========================================"
echo "KernelDash - Build & Deploy"
echo "========================================"

# Build
echo ""
echo "[1/3] Kompiliere Projekt..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo "Fehler beim Build!"
    exit 1
fi

# Publish
echo ""
echo "[2/3] Veröffentliche Release..."
dotnet publish -c Release -o ./publish --self-contained

if [ $? -ne 0 ]; then
    echo "Fehler beim Publish!"
    exit 1
fi

# Done
echo ""
echo "========================================"
echo "✓ Build erfolgreich!"
echo "========================================"
echo ""
echo "Release-Pfad: $(pwd)/publish"
echo ""
