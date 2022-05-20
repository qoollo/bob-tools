#!/bin/bash

function printHelp {
    tools=(
        AliensRecovery
        ClusterModifier
        DisksMonitoring
        RecordsCalculator)
    help_AliensRecovery="Recover aliens in bob cluster"
    help_ClusterModifier="Expand bob cluster, moving data as needed"
    help_DisksMonitoring="Monitor and reconnect disks for single bob node"
    help_RecordsCalculator="Count records in bob cluster"
    echo "Available tools:"
    for item in ${tools[*]}
    do
        text="help_${item}"
        names=($item Bob$item)
        for name in ${names[*]}
        do
            ([[ -x "$(command -v ${name})" ]] || [[ -f $name ]]) && printf "\t%-20s %s\n\n" "$item" "${!text}"
        done
    done
}

[[ $# == 0 ]] && echo "At least one argument is required, use --help to get available tools" && exit
[[ $1 == --help ]] && [[ $# == 1 ]] && printHelp && exit

names=($1 Bob$1 ${1/Bob/})
command=${@:2}
[[ $1 == --help ]] && [[ $# == 2 ]] && names=($2 Bob$2 ${2/Bob/}) && command=--help

for item in ${names[*]}
do
    [[ -x "$(command -v $item)" ]] && $item $command && exit
    [[ -f "$item" ]] && ./$item $command && exit
done
# ./$1 ${@:2}