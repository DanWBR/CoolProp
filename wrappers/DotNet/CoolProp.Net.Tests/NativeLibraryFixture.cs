using System.Runtime.InteropServices;

namespace CoolProp.Net.Tests;

/// <summary>
/// Fails once, readably, when the native library is missing — instead of every
/// test failing with a bare <see cref="DllNotFoundException"/>.
/// </summary>
public sealed class NativeLibraryFixture
{
    public NativeLibraryFixture()
    {
        try
        {
            Version = Information.GetGlobalParamString("version");
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"""
                 The CoolProp native library could not be loaded for RID '{RuntimeInformation.RuntimeIdentifier}'.

                 Build it first:
                   cmake -B build_rid -S . -DCOOLPROP_SHARED_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release
                   cmake --build build_rid --config Release --target install

                 That installs it to install_root/runtimes/<rid>/native/, which this
                 project copies into the test output. In CI, unpack the `runtimes`
                 artifact from library_shared.yml at the repository root instead.
                 """,
                ex);
        }
    }

    /// <summary>The native library version, proving it loaded and answered.</summary>
    public string Version { get; }
}

[CollectionDefinition(Name)]
public sealed class NativeLibraryCollection : ICollectionFixture<NativeLibraryFixture>
{
    public const string Name = "native";
}
