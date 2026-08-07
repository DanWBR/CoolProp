using System.Runtime.InteropServices;
using CoolProp.Interop;

namespace CoolProp;

/// <summary>
/// Top-level property calculations.
/// </summary>
/// <remarks>
/// The class is deliberately not named <c>CoolProp</c>: a type whose name matches
/// its own namespace makes every qualified reference inside that namespace
/// ambiguous.
/// </remarks>
public static class Props
{
    /// <summary>
    /// Evaluates a thermophysical property in SI units.
    /// </summary>
    /// <exception cref="CoolPropException">The native call reported failure.</exception>
    public static double PropsSI(
        string output, string name1, double prop1, string name2, double prop2, string fluidName)
        => Errors.CheckScalar(
            NativeMethods.PropsSI(output, name1, prop1, name2, prop2, fluidName),
            $"PropsSI({output}, {name1}, {name2}, {fluidName})");

    /// <summary>
    /// Evaluates a property that needs no state inputs, such as a critical constant.
    /// </summary>
    public static double Props1SI(string fluidName, string output)
        => Errors.CheckScalar(
            NativeMethods.Props1SI(fluidName, output),
            $"Props1SI({fluidName}, {output})");

    /// <summary>
    /// Evaluates a humid-air property in SI units.
    /// </summary>
    public static double HAPropsSI(
        string output, string name1, double prop1, string name2, double prop2, string name3, double prop3)
        => Errors.CheckScalar(
            NativeMethods.HAPropsSI(output, name1, prop1, name2, prop2, name3, prop3),
            $"HAPropsSI({output}, {name1}, {name2}, {name3})");

    /// <summary>
    /// Returns the phase name at the given state, for example <c>"liquid"</c>.
    /// </summary>
    public static string PhaseSI(
        string name1, double prop1, string name2, double prop2, string fluidName)
    {
        Span<byte> buffer = stackalloc byte[Errors.BufferLength];
        CLong rc = NativeMethods.PhaseSI(name1, prop1, name2, prop2, fluidName, buffer, buffer.Length);

        // The string entry points return 1 on success and 0 on failure, parking
        // the reason in the global error string.
        if (rc.Value == 0)
        {
            throw new CoolPropException(Errors.GetGlobalErrorStringOr(
                $"PhaseSI({name1}, {name2}, {fluidName}) failed and reported no error message."));
        }

        return Errors.DecodeMessage(buffer);
    }

    /// <summary>
    /// Evaluates a saturation ancillary curve directly, bypassing the solver.
    /// </summary>
    public static double SaturationAncillary(
        string fluidName, string output, int quality, string input, double value)
        => Errors.CheckScalar(
            NativeMethods.saturation_ancillary(fluidName, output, quality, input, value),
            $"SaturationAncillary({fluidName}, {output})");

    /// <summary>Specific heat of saturated air, in J/kg/K.</summary>
    public static double CairSat(double temperature)
        => Errors.CheckScalar(NativeMethods.cair_sat(temperature), "CairSat");

    /// <summary>Converts degrees Fahrenheit to Kelvin.</summary>
    public static double FahrenheitToKelvin(double degreesFahrenheit)
        => NativeMethods.F2K(degreesFahrenheit);

    /// <summary>Converts Kelvin to degrees Fahrenheit.</summary>
    public static double KelvinToFahrenheit(double kelvin)
        => NativeMethods.K2F(kelvin);
}
