using System.Runtime.InteropServices;
using CoolProp.Interop;

namespace CoolProp;

/// <summary>
/// Metadata lookups: parameter indices, fluid information and backend parsing.
/// </summary>
public static class Information
{
    /// <summary>
    /// Reads a global parameter string such as <c>"version"</c>, <c>"gitrevision"</c>
    /// or <c>"fluids_list"</c>.
    /// </summary>
    public static string GetGlobalParamString(string param)
    {
        Span<byte> buffer = stackalloc byte[Errors.BufferLength];
        CLong rc = NativeMethods.get_global_param_string(param, buffer, buffer.Length);
        if (rc.Value == 0)
        {
            throw new CoolPropException(
                Errors.GetGlobalErrorStringOr($"get_global_param_string({param}) failed."));
        }

        return Errors.DecodeMessage(buffer);
    }

    /// <summary>Describes a parameter, for example its units or long name.</summary>
    public static string GetParameterInformationString(string param)
    {
        Span<byte> buffer = stackalloc byte[Errors.BufferLength];
        CLong rc = NativeMethods.get_parameter_information_string(param, buffer, buffer.Length);
        if (rc.Value == 0)
        {
            throw new CoolPropException(
                Errors.GetGlobalErrorStringOr($"get_parameter_information_string({param}) failed."));
        }

        return Errors.DecodeMessage(buffer);
    }

    /// <summary>
    /// Reads a fluid parameter string, such as a CAS number or a BibTeX key.
    /// </summary>
    /// <remarks>
    /// The buffer is sized from <c>get_fluid_param_string_len</c> rather than
    /// guessed: values like <c>"JSON"</c> run to tens of kilobytes and would be
    /// truncated by a fixed buffer.
    /// </remarks>
    public static string GetFluidParamString(string fluid, string param)
    {
        CLong needed = NativeMethods.get_fluid_param_string_len(fluid, param);
        if (needed.Value <= 0)
        {
            throw new CoolPropException(
                Errors.GetGlobalErrorStringOr($"get_fluid_param_string_len({fluid}, {param}) failed."));
        }

        // +1 so a value exactly filling the reported length keeps its terminator.
        byte[] buffer = new byte[needed.Value + 1];
        CLong rc = NativeMethods.get_fluid_param_string(fluid, param, buffer, buffer.Length);
        if (rc.Value == 0)
        {
            throw new CoolPropException(
                Errors.GetGlobalErrorStringOr($"get_fluid_param_string({fluid}, {param}) failed."));
        }

        return Errors.DecodeMessage(buffer);
    }

    /// <summary>
    /// Resolves a parameter name to its numeric key, or -1 when unknown.
    /// </summary>
    public static long GetParamIndex(string param) => NativeMethods.get_param_index(param).Value;

    /// <summary>
    /// Resolves an input-pair name such as <c>"PT_INPUTS"</c> to its numeric key,
    /// or -1 when unknown.
    /// </summary>
    public static long GetInputPairIndex(string pair) => NativeMethods.get_input_pair_index(pair).Value;

    /// <summary>Whether the string names a fluid CoolProp can construct.</summary>
    /// <remarks>
    /// The native function returns 1 for valid, 0 for invalid and <b>-1</b> when
    /// the check itself threw, so the test must be <c>== 1</c>: a <c>!= 0</c>
    /// test would report the error case as valid. An internal failure is
    /// reported here as "not valid".
    /// </remarks>
    public static bool IsValidFluidString(string fluidName)
        => NativeMethods.C_is_valid_fluid_string(fluidName) == 1;

    /// <summary>
    /// Splits a fluid string such as <c>"HEOS::Water"</c> into its backend and
    /// fluid parts.
    /// </summary>
    public static (string Backend, string Fluid) ExtractBackend(string fluidString)
    {
        Span<byte> backend = stackalloc byte[Errors.BufferLength];
        Span<byte> fluid = stackalloc byte[Errors.BufferLength];

        // Note the inverted convention: unlike the string getters, this one
        // returns 0 on success and -1 when a buffer was too small.
        int rc = NativeMethods.C_extract_backend(
            fluidString, backend, new CLong(backend.Length), fluid, new CLong(fluid.Length));
        if (rc != 0)
        {
            throw new CoolPropException(
                $"C_extract_backend({fluidString}) failed: a {Errors.BufferLength}-byte buffer was too small.");
        }

        return (Errors.DecodeMessage(backend), Errors.DecodeMessage(fluid));
    }
}
