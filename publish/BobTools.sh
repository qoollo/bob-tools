#!/bin/bash

printHelp() {
    tools=(
        BobAliensRecovery
        BobClusterModifier
        BobDisksMonitoring
        BobRecordsCalculator)
    help_BobAliensRecovery="Recover aliens in bob cluster"
    help_BobClusterModifier="Expand bob cluster, moving data as needed"
    help_BobDisksMonitoring="Monitor and reconnect disks for single bob node"
    help_BobRecordsCalculator="Count records in bob cluster"
    echo "Available tools:"
    for item in ${tools[*]}
    do
        test="help_${item}"
        echo -e "\t$item\t${!test}"
    done
}

[[ $1 == --help ]] && printHelp && exit
echo continue
# ./$1 ${@:2}