using System.Collections.Generic;
using BobApi.BobEntities;

namespace OldPartitionsRemover.UnitTests;

public static class TestConstants
{
    public static readonly ClusterConfiguration DefaultClusterConfiguration =
        new()
        {
            Nodes = new List<ClusterConfiguration.Node>
            {
                new ClusterConfiguration.Node
                {
                    Name = "node1",
                    Address = "localhost",
                    Disks = new List<ClusterConfiguration.Node.Disk>
                    {
                        new ClusterConfiguration.Node.Disk { Name = "disk1", Path = "/" }
                    },
                }
            },
            VDisks = new List<ClusterConfiguration.VDisk>
            {
                new ClusterConfiguration.VDisk
                {
                    Id = 0,
                    Replicas = new List<ClusterConfiguration.VDisk.Replica>
                    {
                        new ClusterConfiguration.VDisk.Replica { Disk = "disk1", Node = "node1" }
                    }
                }
            }
        };
}
