using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using BobApi;
using BobApi.BobEntities;
using BobApi.Entities;
using BobToolsCli.BobApliClientFactories;
using FakeItEasy;

namespace OldPartitionsRemover.UnitTests.Customizations;

public class FrozenApiClientsCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize(new AutoFakeItEasyCustomization());

        var factory = fixture.Freeze<IBobApiClientFactory>();

        var partitionsBobApiClient = fixture.Freeze<IPartitionsBobApiClient>();
        A.CallTo(() => factory.GetPartitionsBobApiClient(A<ClusterConfiguration.Node>.Ignored))
            .Returns(partitionsBobApiClient);
        A.CallTo(partitionsBobApiClient).WithReturnType<Task<BobApiResult<List<string>>>>()
            .Returns(BobApiResult<List<string>>.Ok(new List<string>()));

        var spaceBobApiClient = fixture.Freeze<ISpaceBobApiClient>();
        A.CallTo(() => factory.GetSpaceBobApiClient(A<ClusterConfiguration.Node>.Ignored))
            .Returns(spaceBobApiClient);

    }
}