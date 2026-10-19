using System.Reflection;
using System.Reflection.Emit;
using JiuMi.Infrastructure;

namespace JiuMi.Infrastructure.Tests;

public sealed class AssemblyApplicationInfoTests
{
    [Fact]
    public void Name_GivenProductAttribute_ReturnsProduct()
    {
        Assembly assembly = CreateAssembly("JiuMi Test", "1.2.3+abcdef");
        var info = new AssemblyApplicationInfo(assembly);

        Assert.Equal("JiuMi Test", info.Name);
    }

    [Fact]
    public void Version_GivenInformationalVersionWithBuildMetadata_ReturnsSemanticVersion()
    {
        Assembly assembly = CreateAssembly("JiuMi Test", "1.2.3-beta.1+abcdef");
        var info = new AssemblyApplicationInfo(assembly);

        Assert.Equal("1.2.3-beta.1", info.Version);
    }

    [Fact]
    public void Version_GivenNoInformationalVersion_ReturnsUnknownVersion()
    {
        Assembly assembly = CreateAssembly("JiuMi Test", informationalVersion: null);
        var info = new AssemblyApplicationInfo(assembly);

        Assert.Equal("0.0.0", info.Version);
    }

    private static AssemblyBuilder CreateAssembly(string product, string? informationalVersion)
    {
        AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"JiuMi.TestAssembly.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        builder.SetCustomAttribute(CreateStringAttribute<AssemblyProductAttribute>(product));

        if (informationalVersion is not null)
        {
            builder.SetCustomAttribute(CreateStringAttribute<AssemblyInformationalVersionAttribute>(informationalVersion));
        }

        return builder;
    }

    private static CustomAttributeBuilder CreateStringAttribute<TAttribute>(string value)
        where TAttribute : Attribute
    {
        ConstructorInfo constructor =
            typeof(TAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException($"{typeof(TAttribute)} has no single-string constructor.");

        return new CustomAttributeBuilder(constructor, [value]);
    }
}
