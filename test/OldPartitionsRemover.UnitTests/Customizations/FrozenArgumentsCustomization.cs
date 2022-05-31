using System;
using AutoFixture;
using BobToolsCli;

namespace OldPartitionsRemover.UnitTests.Customizations;

public class FrozenArgumentsCustomization<T> : ICustomization
where T : CommonArguments
{
    private readonly Action<T>? _setup;

    public FrozenArgumentsCustomization(Action<T>? setup = null)
    {
        _setup = setup;
    }

    public void Customize(IFixture fixture)
    {
        var arguments = fixture.Freeze<T>();
        _setup?.Invoke(arguments);
        fixture.Inject<CommonArguments>(arguments);
    }
}