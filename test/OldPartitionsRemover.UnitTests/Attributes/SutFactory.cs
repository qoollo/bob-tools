using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using AutoFixture.NUnit3;
using BobApi;
using BobApi.BobEntities;
using BobToolsCli;
using BobToolsCli.BobApliClientFactories;
using BobToolsCli.ConfigurationFinding;
using FakeItEasy;

namespace OldPartitionsRemover.UnitTests.Attributes;

public class SutFactory : AutoDataAttribute
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


    public SutFactory() : base(() =>
    {
        var fixture = new Fixture();
        fixture.Customize(new AutoFakeItEasyCustomization());

        var factory = fixture.Freeze<IBobApiClientFactory>();
        var partitionsBobApiClient = fixture.Freeze<IPartitionsBobApiClient>();
        A.CallTo(() => factory.GetPartitionsBobApiClient(A<ClusterConfiguration.Node>.Ignored))
            .Returns(partitionsBobApiClient);

        var configurationFinder = fixture.Freeze<IConfigurationFinder>();
        A.CallTo<Task<YamlReadingResult<ClusterConfiguration>>>(() => configurationFinder.FindClusterConfiguration(A<CancellationToken>.Ignored))
            .Returns(YamlReadingResult<ClusterConfiguration>.Ok(s_defaultConfiguration));

        return fixture;
    })
    { }
}