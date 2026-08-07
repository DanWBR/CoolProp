using System.Runtime.InteropServices;
using CoolProp.Interop;

namespace CoolProp;

/// <summary>
/// Process-wide CoolProp configuration and fluid registration.
/// </summary>
/// <remarks>
/// These settings are global to the loaded native library, not scoped to a
/// state object.
/// </remarks>
public static class Config
{
    /// <summary>Sets a string configuration key.</summary>
    public static void SetString(string key, string value) => NativeMethods.set_config_string(key, value);

    /// <summary>Sets a numeric configuration key.</summary>
    public static void SetDouble(string key, double value) => NativeMethods.set_config_double(key, value);

    /// <summary>Sets a boolean configuration key.</summary>
    public static void SetBool(string key, bool value) => NativeMethods.set_config_bool(key, value);

    /// <summary>Verbosity of the native debug output.</summary>
    public static int DebugLevel
    {
        get => NativeMethods.get_debug_level();
        set => NativeMethods.set_debug_level(value);
    }

    /// <summary>
    /// Sets the reference state of a fluid to a named convention, for example
    /// <c>"IIR"</c>, <c>"ASHRAE"</c> or <c>"NBP"</c>.
    /// </summary>
    public static void SetReferenceState(string fluid, string referenceState)
    {
        if (NativeMethods.set_reference_stateS(fluid, referenceState) == 0)
        {
            throw new CoolPropException(Errors.GetGlobalErrorStringOr(
                $"set_reference_stateS({fluid}, {referenceState}) failed."));
        }
    }

    /// <summary>
    /// Sets the reference state of a fluid from an explicit state point.
    /// </summary>
    public static void SetReferenceState(
        string fluid, double temperature, double molarDensity, double molarEnthalpy, double molarEntropy)
    {
        if (NativeMethods.set_reference_stateD(
                fluid, temperature, molarDensity, molarEnthalpy, molarEntropy) == 0)
        {
            throw new CoolPropException(
                Errors.GetGlobalErrorStringOr($"set_reference_stateD({fluid}) failed."));
        }
    }

    /// <summary>Registers additional fluids supplied as a JSON string.</summary>
    public static void AddFluidsAsJson(string backend, string fluidJson)
    {
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.add_fluids_as_JSON(
            backend, fluidJson, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Registers departure functions supplied as a JSON string.</summary>
    public static void SetDepartureFunctions(string json)
    {
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.set_departure_functions(
            json, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>
    /// Redirects native stdout to a file. Pass an empty string to restore.
    /// </summary>
    public static void RedirectStdout(string file) => NativeMethods.redirect_stdout(file);
}
