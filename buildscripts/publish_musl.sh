#! /bin/sh
VERSION=$(git describe --tags | sed -r 's/v([0-9].[0-9].[0-9]).*/\1/g')
INFORMATIONALVERSION=$(git describe --tags)
CONFIGURATION=Release
RUNTIME=linux-musl-x64
echo $VERSION
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/DisksMonitoring
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/OldPartitionsRemover
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/RecordsCalculator
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/DiskStatusAnalyzer
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/ClusterModifier
dotnet publish -c $CONFIGURATION -r $RUNTIME -p:Version=$VERSION -p:InformationalVersion=$INFORMATIONALVERSION -o publish src/BobAliensRecovery