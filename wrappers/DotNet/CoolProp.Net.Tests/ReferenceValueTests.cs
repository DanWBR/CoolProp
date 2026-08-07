namespace CoolProp.Net.Tests;

/// <summary>
/// Checks the binding against values that do not come from the binding.
/// </summary>
/// <remarks>
/// The critical constants below are the defining values of the reference
/// equations of state — IAPWS-95 for water, Span-Wagner for CO2, Span et al.
/// for nitrogen, Tillner-Roth &amp; Baehr for R134a. Asserting against numbers
/// captured from a previous run of this same binding would only enshrine
/// whatever it currently does, including a marshalling bug.
/// </remarks>
[Collection(NativeLibraryCollection.Name)]
public sealed class ReferenceValueTests
{
    /// <remarks>
    /// R134a gets a looser tolerance on purpose. <c>Tcrit</c> is the critical
    /// point of the equation of state, which for R134a sits about 2 mK above the
    /// 374.21 K its own fluid file tabulates; the other three agree with the
    /// published constant to well under a millikelvin. Loosening all four to hide
    /// that would throw away the precision of the cases that are exact.
    /// </remarks>
    [Theory]
    [InlineData("Water", 647.096, 3)]      // IAPWS-95
    [InlineData("CO2", 304.1282, 3)]       // Span & Wagner (1996)
    [InlineData("Nitrogen", 126.192, 3)]   // Span et al. (2000)
    [InlineData("R134a", 374.21, 2)]       // Tillner-Roth & Baehr (1994)
    public void CriticalTemperatureMatchesTheReferenceEquation(string fluid, double expected, int precision)
    {
        Assert.Equal(expected, Props.Props1SI(fluid, "Tcrit"), precision);
    }

    [Fact]
    public void WaterCriticalPressureMatchesIapws95()
    {
        Assert.InRange(Props.Props1SI("Water", "pcrit"), 22.0635e6, 22.0645e6);
    }

    /// <summary>Normal boiling point of water: the definition of 100 °C.</summary>
    [Fact]
    public void WaterBoilsAt373KelvinAtOneAtmosphere()
    {
        Assert.Equal(373.1243, Props.PropsSI("T", "P", 101325, "Q", 0, "Water"), 3);
    }

    /// <summary>Liquid water at 300 K, 1 atm — a standard textbook value.</summary>
    [Fact]
    public void LiquidWaterDensityAt300Kelvin()
    {
        Assert.Equal(996.56, Props.PropsSI("D", "T", 300, "P", 101325, "Water"), 1);
    }

    [Fact]
    public void WaterIsLiquidAt300KelvinAndOneAtmosphere()
    {
        Assert.Contains("liquid", Props.PhaseSI("T", 300, "P", 101325, "Water"));
    }

    [Fact]
    public void CarbonDioxideIsSupercriticalAbovaItsCriticalPoint()
    {
        string phase = Props.PhaseSI("T", 320, "P", 10e6, "CO2");
        Assert.Contains("supercritical", phase);
    }

    /// <summary>
    /// Humid air at 25 °C, 1 atm, 50 % RH: ≈ 50.4 kJ per kg of dry air in any
    /// psychrometric table.
    /// </summary>
    [Fact]
    public void HumidAirEnthalpyMatchesPsychrometricTables()
    {
        double h = Props.HAPropsSI("H", "T", 298.15, "P", 101325, "R", 0.5);
        Assert.InRange(h, 50.0e3, 50.9e3);
    }

    /// <summary>
    /// An aqueous 20 % ethylene-glycol solution is denser than pure water. This
    /// is a range check on the INCOMP backend, not a reference value.
    /// </summary>
    [Fact]
    public void IncompressibleGlycolIsDenserThanWater()
    {
        double density = Props.PropsSI("D", "T", 300, "P", 101325, "INCOMP::MEG-20%");
        Assert.InRange(density, 1000.0, 1100.0);
    }

    [Fact]
    public void MixtureCompositionRoundTripsAndSolves()
    {
        using AbstractState state = AbstractState.Create("HEOS", "Methane&Ethane");
        state.SetFractions([0.7, 0.3]);

        Assert.Equal([0.7, 0.3], state.MoleFractions());

        state.Update("PT_INPUTS", 101325, 300);
        Assert.True(state.KeyedOutput("Dmolar") > 0);
    }

    [Fact]
    public void AbstractStateAgreesWithPropsSI()
    {
        using AbstractState state = AbstractState.Create("HEOS", "Water");
        state.Update("PT_INPUTS", 101325, 300);

        double viaState = state.KeyedOutput("D");
        double viaProps = Props.PropsSI("D", "T", 300, "P", 101325, "Water");

        Assert.Equal(viaProps, viaState, 6);
    }

    [Fact]
    public void BackendNameIdentifiesTheHelmholtzBackend()
    {
        using AbstractState state = AbstractState.Create("HEOS", "Water");
        Assert.Contains("Helmholtz", state.BackendName());
    }

    [Fact]
    public void ExtractBackendSplitsAQualifiedFluidString()
    {
        Assert.Equal(("HEOS", "Water"), Information.ExtractBackend("HEOS::Water"));
    }

    [Fact]
    public void FluidValidityIsReportedCorrectly()
    {
        Assert.True(Information.IsValidFluidString("Water"));
        Assert.False(Information.IsValidFluidString("ThisIsNotAFluid&&&"));
    }
}
