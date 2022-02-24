using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using BobApi.BobEntities;
using BobToolsCli.ConfigurationFinding;
using BobToolsCli.ConfigurationReading;
using FakeItEasy;

namespace OldPartitionsRemover.UnitTests.Customizations;

public class SingleNodeConfigCustomization : ICustomization
{
    private static readonly ClusterConfiguration s_defaultConfiguration = new()
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
                        new ClusterConfiguration.VDisk.Replica
                        {
                            Disk = "disk1",
                            Node = "node1"
                        }
                    }
                }
            }
    };

    public void Customize(IFixture fixture)
    {
        fixture.Customize(new AutoFakeItEasyCustomization());

        var configurationFinder = fixture.Freeze<IConfigurationFinder>();
        A.CallTo(configurationFinder).WithReturnType<Task<ConfigurationReadingResult<ClusterConfiguration>>>()
            .Returns(ConfigurationReadingResult<ClusterConfiguration>.Ok(s_defaultConfiguration));
    }
}