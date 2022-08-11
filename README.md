# Bob tools

Various tools for interacting with [bob](https://github.com/qoollo/bob)

## Records calculator

Tool for counting all unique records and replicas in bob.

## Old partitions remover

Tool for removing all partitions in cluster older than provided date.

## Disks monitoring

Tool for discovery, formatting and mounting of new disks.

## BobAliensRecovery

Tool for recovering aliens from nodes.

## Cluster modifier

Tool for expanding clusters according to configurations.

### Dockerized

Requires ssh key to be available to run.

### Requirements

- rsync (with xxh128 support)
- sshd
- find
- xxhash

### Building dockers

To build docker images simply pass appropriate dockerfile from `dockerfiles` dir, using root folder as context. E.g. `docker build -f dockerfiles/RecordsCalculatorDockerfile .`.
