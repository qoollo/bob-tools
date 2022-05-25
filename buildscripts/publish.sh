#! /bin/sh
VERSION=${1:-$(git describe --tags | sed -r 's/v([0-9].[0-9].[0-9]).*/\1/g')}
INFORMATIONALVERSION=${2:-$(git describe --tags)}
CONFIGURATION=Release
RUNTIME=${3:-linux-x64}

echo $VERSION
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/DisksMonitoring
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/OldPartitionsRemover
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/RecordsCalculator
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/DiskStatusAnalyzer
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/ClusterModifier
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/BobAliensRecovery
