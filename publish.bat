@echo off
echo Publishing Terraria-RPC as a self-contained 32-bit executable...
dotnet publish TerrariaRPC.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o bin/Publish
echo.
echo Publish complete! The standalone executable is located in: bin/Publish
