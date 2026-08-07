using System.Runtime.InteropServices;

namespace CoolProp.Net.Tests;

/// <summary>
/// Guards the marshalling decisions that fail silently rather than loudly.
/// </summary>
/// <remarks>
/// A wrong <c>long</c> mapping compiles, and passes on a Windows-only run,
/// because C <c>long</c> happens to be 4 bytes there. These tests exist to fail
/// on the LP64 targets (linux-arm64, linux-x64, osx-*) where it is 8.
/// </remarks>
[Collection(NativeLibraryCollection.Name)]
public sealed class AbiTests(NativeLibraryFixture fixture)
{
    [Fact]
    public void CLongMatchesPlatformWidth()
    {
        int expected = OperatingSystem.IsWindows() ? 4 : 8;
        Assert.Equal(expected, Marshal.SizeOf<CLong>());
    }

    [Fact]
    public void NativeLibraryReportsAVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(fixture.Version));
    }

    [Fact]
    public void ParamIndexResolvesKnownName()
    {
        Assert.True(Information.GetParamIndex("T") >= 0);
    }

    /// <summary>
    /// The sharpest ABI probe available: the native side returns exactly -1 for
    /// an unknown name. A mis-sized return marshal yields garbage or a truncated
    /// value, not -1.
    /// </summary>
    [Fact]
    public void ParamIndexReturnsMinusOneForUnknownName()
    {
        Assert.Equal(-1, Information.GetParamIndex("definitely_not_a_parameter"));
    }

    [Fact]
    public void InputPairIndexResolvesKnownName()
    {
        Assert.True(Information.GetInputPairIndex("PT_INPUTS") >= 0);
    }

    [Fact]
    public void InputPairIndexReturnsMinusOneForUnknownName()
    {
        Assert.Equal(-1, Information.GetInputPairIndex("definitely_not_a_pair"));
    }

    /// <summary>
    /// Handles are table indices starting at 0, and 0 must not be mistaken for
    /// an invalid handle. Allocating several at once also proves that a handle
    /// above 0 still round-trips through the CLong boundary.
    /// </summary>
    [Fact]
    public void ManyConcurrentHandlesRemainUsable()
    {
        var states = new List<AbstractState>();
        try
        {
            for (int i = 0; i < 8; i++)
            {
                AbstractState state = AbstractState.Create("HEOS", "Water");
                state.Update("PT_INPUTS", 101325, 300);
                Assert.True(state.KeyedOutput("Dmolar") > 0);
                states.Add(state);
            }
        }
        finally
        {
            foreach (AbstractState state in states)
            {
                state.Dispose();
            }
        }
    }

    /// <summary>
    /// <c>set_config_bool</c> takes a one-byte C++ <c>bool</c>, bound with
    /// <c>UnmanagedType.U1</c>. There is no config getter in the C API, so the
    /// value is checked by its effect: with property-limit checking disabled, a
    /// state below water's minimum temperature resolves instead of throwing.
    /// </summary>
    [Fact]
    public void BoolConfigTakesEffect()
    {
        // Below the 273.16 K lower limit of the water EOS.
        Assert.Throws<CoolPropException>(
            () => Props.PropsSI("D", "T", 250, "P", 101325, "Water"));

        Config.SetBool("DONT_CHECK_PROPERTY_LIMITS", true);
        try
        {
            Assert.True(Props.PropsSI("D", "T", 250, "P", 101325, "Water") > 0);
        }
        finally
        {
            Config.SetBool("DONT_CHECK_PROPERTY_LIMITS", false);
        }

        // The false path must restore the original behaviour, otherwise this
        // test would be passing on a stuck flag rather than on the value sent.
        Assert.Throws<CoolPropException>(
            () => Props.PropsSI("D", "T", 250, "P", 101325, "Water"));
    }
}
