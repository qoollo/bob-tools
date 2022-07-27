#! /bin/sh
CONFIGURATION=Release
RUNTIME=${1:-linux-x64}

echo $VERSION
dotnet publish -c $CONFIGURATION -r $RUNTIME -o publish src/DisksMonitoring
dotnet publish -c $CONFIGURATION -r $RUNTIME -o publish src/OldPartitionsRemover
dotnet publish -c $CONFIGURATION -r $RUNTIME -o publish src/RecordsCalculator
dotnet publish -c $CONFIGURATION -r $RUNTIME -o publish src/ClusterModifier
dotnet publish -c $CONFIGURATION -r $RUNTIME -o publish src/BobAliensRecovery
