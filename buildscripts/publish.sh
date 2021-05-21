#! /bin/sh
VERSION=$(git describe --tags)
CONFIGURATION=Release
RUNTIME=linux-x64
echo $VERSION
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -o publish src/DisksMonitoring
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -o publish src/OldPartitionsRemover
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -o publish src/RecordsCalculator
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -o publish src/DiskStatusAnalyzer
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -o publish src/ClusterModifier