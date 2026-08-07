using System.Runtime.InteropServices;

namespace CoolProp.Interop;

/// <summary>
/// P/Invoke declarations for the flat C API in <c>include/CoolProp/CoolPropLib.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every C <c>long</c> is bound as <see cref="CLong"/>.</b> C <c>long</c> is
/// 4 bytes on Windows (LLP64) and 8 bytes on Linux/macOS (LP64); binding it as
/// <c>int</c> corrupts arguments on Unix and binding it as <c>long</c> corrupts
/// them on Windows. Neither mistake fails to compile, and neither shows up on a
/// Windows-only test run — it surfaces as corrupted handles on exactly the
/// arm64/Linux targets this binding exists to support. C <c>int</c> stays
/// <c>int</c>: the header uses both, and the distinction is load-bearing.
/// </para>
/// <para>
/// Calling convention is cdecl on every 64-bit target: <c>CMakeLists.txt</c>
/// forces <c>CONVENTION</c> empty for <c>BITNESS=64</c> and passes it as
/// <c>-DCONVENTION=</c>. 32-bit Windows, the only build where <c>CONVENTION</c>
/// becomes <c>__stdcall</c>, is out of scope.
/// </para>
/// <para>
/// The Fortran shims <c>propssi_</c>, <c>hapropssi_</c> and <c>haprops_</c> are
/// deliberately not bound: they exist for Fortran's calling convention and have
/// no use from .NET.
/// </para>
/// </remarks>
internal static partial class NativeMethods
{
    /// <summary>
    /// Resolves to <c>CoolProp.dll</c>, <c>libCoolProp.so</c> or
    /// <c>libCoolProp.dylib</c> through the default probing rules, matching
    /// <c>COOLPROP_LIBRARY_NAME</c> in <c>CMakeLists.txt</c>.
    /// </summary>
    private const string Lib = "CoolProp";

    // ---------------------------------------------------------------- scalars

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double Props1SI(string fluidName, string output);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double PropsSI(
        string output, string name1, double prop1, string name2, double prop2, string fluidName);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double HAPropsSI(
        string output, string name1, double prop1, string name2, double prop2, string name3, double prop3);

    [LibraryImport(Lib)]
    internal static partial double cair_sat(double t);

    [LibraryImport(Lib)]
    internal static partial double F2K(double tF);

    [LibraryImport(Lib)]
    internal static partial double K2F(double tK);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double saturation_ancillary(
        string fluidName, string output, int q, string input, double value);

    // `resdim1` and `resdim2` are in/out: the caller passes the allocated size
    // and the callee overwrites it with the size actually used.
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void Props1SImulti(
        string outputs, string backend, string fluidNames, ReadOnlySpan<double> fractions,
        CLong lengthFractions, Span<double> result, ref CLong resdim1);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void PropsSImulti(
        string outputs, string name1, ReadOnlySpan<double> prop1, CLong sizeProp1,
        string name2, ReadOnlySpan<double> prop2, CLong sizeProp2,
        string backend, string fluidNames, ReadOnlySpan<double> fractions, CLong lengthFractions,
        Span<double> result, ref CLong resdim1, ref CLong resdim2);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong PhaseSI(
        string name1, double prop1, string name2, double prop2, string fluidName, Span<byte> phase, int n);

    // ---------------------------------------------------------------- strings

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_global_param_string(string param, Span<byte> output, int n);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_parameter_information_string(string param, Span<byte> output, int n);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_fluid_param_string(string fluid, string param, Span<byte> output, int n);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_fluid_param_string_len(string fluid, string param);

    // ---------------------------------------------------------------- indices

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_param_index(string param);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_input_pair_index(string pair);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong redirect_stdout(string file);

    [LibraryImport(Lib)]
    internal static partial int get_debug_level();

    [LibraryImport(Lib)]
    internal static partial void set_debug_level(int level);

    // ----------------------------------------------------------- config/state

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void set_config_string(string key, string val);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void set_config_double(string key, double val);

    // C++ `bool` is one byte; without U1 the default marshalling widens it to a
    // 4-byte Win32 BOOL and corrupts the argument.
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void set_config_bool(string key, [MarshalAs(UnmanagedType.U1)] bool val);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int set_reference_stateS(string reference, string referenceState);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int set_reference_stateD(
        string reference, double t, double rhomolar, double hmolar0, double smolar0);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void set_departure_functions(
        string stringData, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void add_fluids_as_JSON(
        string backend, string fluidstring, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int C_is_valid_fluid_string(string fluidName);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int C_extract_backend(
        string fluidString, Span<byte> backend, CLong backendLength, Span<byte> fluid, CLong fluidLength);

    // ---------------------------------------------------- AbstractState: life

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong AbstractState_factory(
        string backend, string fluids, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_free(
        CLong handle, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_fluid_names(
        CLong handle, Span<byte> fluids, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_backend_name(
        CLong handle, Span<byte> backend, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_fluid_param_string(
        CLong handle, string param, Span<byte> returnBuffer, CLong returnBufferLength,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ------------------------------------------------- AbstractState: updates

    [LibraryImport(Lib)]
    internal static partial void AbstractState_update(
        CLong handle, CLong inputPair, double value1, double value2,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_specify_phase(
        CLong handle, string phase, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_unspecify_phase(
        CLong handle, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial int AbstractState_phase(
        CLong handle, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_set_fractions(
        CLong handle, ReadOnlySpan<double> fractions, CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_mole_fractions(
        CLong handle, Span<double> fractions, CLong maxN, out CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_get_mole_fractions_satState(
        CLong handle, string saturatedState, Span<double> fractions, CLong maxN, out CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ------------------------------------------------- AbstractState: outputs

    [LibraryImport(Lib)]
    internal static partial double AbstractState_keyed_output(
        CLong handle, CLong param, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double AbstractState_keyed_output_satState(
        CLong handle, string saturatedState, CLong param,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_saturated_liquid_keyed_output(
        CLong handle, CLong param, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_saturated_vapor_keyed_output(
        CLong handle, CLong param, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // --------------------------------------------- AbstractState: derivatives

    [LibraryImport(Lib)]
    internal static partial double AbstractState_first_saturation_deriv(
        CLong handle, CLong of, CLong wrt, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_first_partial_deriv(
        CLong handle, CLong of, CLong wrt, CLong constant,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_second_partial_deriv(
        CLong handle, CLong of1, CLong wrt1, CLong constant1, CLong wrt2, CLong constant2,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_second_two_phase_deriv(
        CLong handle, CLong of1, CLong wrt1, CLong constant1, CLong wrt2, CLong constant2,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_first_two_phase_deriv(
        CLong handle, CLong of, CLong wrt, CLong constant,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_first_two_phase_deriv_splined(
        CLong handle, CLong of, CLong wrt, CLong constant, double xEnd,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ------------------------------------------------ AbstractState: batching

    [LibraryImport(Lib)]
    internal static partial void AbstractState_update_and_common_out(
        CLong handle, CLong inputPair, ReadOnlySpan<double> value1, ReadOnlySpan<double> value2, CLong length,
        Span<double> t, Span<double> p, Span<double> rhomolar, Span<double> hmolar, Span<double> smolar,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_update_and_1_out(
        CLong handle, CLong inputPair, ReadOnlySpan<double> value1, ReadOnlySpan<double> value2, CLong length,
        CLong output, Span<double> @out,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_update_and_5_out(
        CLong handle, CLong inputPair, ReadOnlySpan<double> value1, ReadOnlySpan<double> value2, CLong length,
        ReadOnlySpan<CLong> outputs,
        Span<double> out1, Span<double> out2, Span<double> out3, Span<double> out4, Span<double> out5,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ---------------------------------------------- AbstractState: parameters

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_set_binary_interaction_double(
        CLong handle, CLong i, CLong j, string parameter, double value,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_set_cubic_alpha_C(
        CLong handle, CLong i, string parameter, double c1, double c2, double c3,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_set_fluid_parameter_double(
        CLong handle, CLong i, string parameter, double value,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ------------------------------------------------ AbstractState: fugacity

    [LibraryImport(Lib)]
    internal static partial double AbstractState_get_fugacity(
        CLong handle, CLong i, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_get_fugacity_coefficient(
        CLong handle, CLong i, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_get_chemical_potential(
        CLong handle, CLong i, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_fugacities(
        CLong handle, Span<double> values, CLong maxN, out CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_fugacity_coefficients(
        CLong handle, Span<double> values, CLong maxN, out CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_chemical_potentials(
        CLong handle, Span<double> values, CLong maxN, out CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ------------------------------------------------ AbstractState: envelope

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void AbstractState_build_phase_envelope(
        CLong handle, string level, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_phase_envelope_data(
        CLong handle, CLong length,
        Span<double> t, Span<double> p, Span<double> rhomolarVap, Span<double> rhomolarLiq,
        Span<double> x, Span<double> y,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_phase_envelope_data_checkedMemory(
        CLong handle, CLong length, CLong maxComponents,
        Span<double> t, Span<double> p, Span<double> rhomolarVap, Span<double> rhomolarLiq,
        Span<double> x, Span<double> y, out CLong actualLength, out CLong actualComponents,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_build_spinodal(
        CLong handle, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_get_spinodal_data(
        CLong handle, CLong length, Span<double> tau, Span<double> delta, Span<double> m1,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_all_critical_points(
        CLong handle, CLong length,
        Span<double> t, Span<double> p, Span<double> rhomolar, Span<CLong> stable,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ------------------------------------------------------------- deprecated

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double PropsS(
        string output, string name1, double prop1, string name2, double prop2, string reference);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double Props1(string fluidName, string output);

    /// <summary>Deprecated non-SI variant.</summary>
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double HAProps(
        string output, string name1, double prop1, string name2, double prop2, string name3, double prop3);

    /// <summary>
    /// Deprecated. <c>name1</c> and <c>name2</c> are single C <c>char</c> values
    /// passed by value, not strings — hence <see cref="byte"/>.
    /// </summary>
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double Props(
        string output, byte name1, double prop1, byte name2, double prop2, string reference);
}
