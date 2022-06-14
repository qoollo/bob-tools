#!/bin/bash

function printHelp {
    tools=(
        AliensRecovery
        ClusterModifier
        DisksMonitoring
        RecordsCalculator
        OldPartitionsRemover
        brt
        bobp
        ccg)
    help_AliensRecovery="Recover aliens in bob cluster"
    help_ClusterModifier="Expand bob cluster"
    help_DisksMonitoring="Monitor and reconnect disks for single bob node"
    help_RecordsCalculator="Count records in bob cluster"
    help_OldPartitionsRemover="Remove partitions based on various filters"
    help_brt=""
    help_bobp="Read and write records to cluster"
    help_ccg="Create cluster configuration"
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

# First is executable name, others are aliases
ar=(BobAliensRecovery BobAliensRecovery AliensRecovery)
cm=(ClusterModifier BobClusterModifier ClusterModifier)
dm=(DisksMonitoring BobDisksMonitoring DisksMonitoring)
rc=(RecordsCalculator BobRecordsCalculator RecordsCalculator)
opr=(OldPartitionsRemover BobOldPartitionsRemover OldPartitionsRemover)
brt=(brt brt)
bobp=(bobp bobp)
ccg=(ccg ccg)
possible_names=('ar[@]' 'cm[@]' 'dm[@]' 'rc[@]' 'opr[@]' 'brt[@]' 'bobp[@]' 'ccg[@]')

declare name
for arr in ${possible_names[*]}
do
    loc=${!arr:0:1}
    for item in ${!arr:1}
    do
        [[ $1 == $item ]] && name="${loc}"
    done
done
command=${@:2}

[[ -x "$(command -v $name)" ]] && $name $command && exit
[[ -f "$name" ]] && ./$name $command && exit