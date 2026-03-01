#!/bin/bash
# Build and pack SatoshiTickets as an installable BTCPay Server plugin (.btcpay)

set -e

PLUGIN_NAME="BTCPayServer.Plugins.SatoshiTickets"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_DIR="$SCRIPT_DIR/bin/Release/net8.0"
OUTPUT_DIR="${1:-$REPO_ROOT/dist}"

DOTNET="${DOTNET:-dotnet}"
if ! command -v "$DOTNET" &>/dev/null; then
    if [ -x "$HOME/.dotnet/dotnet" ]; then
        DOTNET="$HOME/.dotnet/dotnet"
    else
        echo "Error: dotnet not found. Install .NET 8 SDK or set DOTNET path."
        exit 1
    fi
fi

echo "Building $PLUGIN_NAME..."
$DOTNET build "$SCRIPT_DIR/$PLUGIN_NAME.csproj" -c Release

echo "Packing plugin..."
cd "$REPO_ROOT/btcpayserver/BTCPayServer.PluginPacker"
$DOTNET run -- "$BUILD_DIR" "$PLUGIN_NAME" "$OUTPUT_DIR"

PLUGIN_VERSION=$(ls -1 "$OUTPUT_DIR/$PLUGIN_NAME/" 2>/dev/null | head -1)
echo ""
echo "Done! Installable plugin created at:"
echo "  $OUTPUT_DIR/$PLUGIN_NAME/$PLUGIN_VERSION/$PLUGIN_NAME.btcpay"
echo ""
echo "To install: Upload this file via BTCPay Server > Settings > Plugins"
