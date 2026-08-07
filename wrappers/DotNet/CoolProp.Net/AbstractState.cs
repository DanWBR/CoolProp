using System.Runtime.InteropServices;
using CoolProp.Interop;

namespace CoolProp;

/// <summary>
/// Points sampled along a phase envelope.
/// </summary>
/// <param name="Temperature">Temperature at each point, K.</param>
/// <param name="Pressure">Pressure at each point, Pa.</param>
/// <param name="VaporMolarDensity">Vapour molar density at each point, mol/m³.</param>
/// <param name="LiquidMolarDensity">Liquid molar density at each point, mol/m³.</param>
/// <param name="LiquidMoleFractions">Liquid composition, indexed [point, component].</param>
/// <param name="VaporMoleFractions">Vapour composition, indexed [point, component].</param>
public sealed record PhaseEnvelope(
    double[] Temperature,
    double[] Pressure,
    double[] VaporMolarDensity,
    double[] LiquidMolarDensity,
    double[,] LiquidMoleFractions,
    double[,] VaporMoleFractions);

/// <summary>
/// A critical point reported by <see cref="AbstractState.AllCriticalPoints"/>.
/// </summary>
public readonly record struct CriticalPoint(
    double Temperature, double Pressure, double MolarDensity, bool Stable);

/// <summary>
/// A CoolProp state object: a fluid plus the equation-of-state backend that
/// evaluates it.
/// </summary>
public sealed class AbstractState : IDisposable
{
    private readonly AbstractStateHandle _handle;

    private AbstractState(AbstractStateHandle handle) => _handle = handle;

    /// <summary>
    /// Constructs a state, for example <c>Create("HEOS", "Water")</c> or
    /// <c>Create("HEOS", "Methane&amp;Ethane")</c>.
    /// </summary>
    /// <exception cref="CoolPropException">The backend or fluid was rejected.</exception>
    public static AbstractState Create(string backend, string fluids)
    {
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        CLong handle = NativeMethods.AbstractState_factory(
            backend, fluids, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);

        return new AbstractState(AbstractStateHandle.FromNative(handle));
    }

    public void Dispose() => _handle.Dispose();

    /// <summary>
    /// Takes a ref count on the handle for the duration of a native call, so the
    /// finalizer cannot free the state underneath an in-flight call.
    /// </summary>
    private Lease Acquire()
    {
        ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
        return new Lease(_handle);
    }

    private readonly struct Lease : IDisposable
    {
        private readonly AbstractStateHandle _handle;

        internal Lease(AbstractStateHandle handle)
        {
            _handle = handle;
            bool acquired = false;
            handle.DangerousAddRef(ref acquired);
            if (!acquired)
            {
                throw new ObjectDisposedException(nameof(AbstractState));
            }

            Value = handle.DangerousGetCLong();
        }

        internal CLong Value { get; }

        public void Dispose() => _handle.DangerousRelease();
    }

    // ------------------------------------------------------------------ state

    /// <summary>
    /// Updates the state from a named input pair, for example <c>"PT_INPUTS"</c>.
    /// </summary>
    public void Update(string inputPair, double value1, double value2)
        => Update(ResolveInputPair(inputPair), value1, value2);

    /// <summary>Updates the state from a resolved input-pair key.</summary>
    public void Update(long inputPair, double value1, double value2)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_update(
            lease.Value, new CLong((nint)inputPair), value1, value2,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Sets the mole (or mass) fractions of a mixture.</summary>
    public void SetFractions(ReadOnlySpan<double> fractions)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_set_fractions(
            lease.Value, fractions, new CLong(fractions.Length),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Forces the phase, bypassing phase detection.</summary>
    public void SpecifyPhase(string phase)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_specify_phase(
            lease.Value, phase, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Restores automatic phase detection.</summary>
    public void UnspecifyPhase()
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_unspecify_phase(
            lease.Value, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>The phase index of the current state.</summary>
    public int Phase
    {
        get
        {
            using Lease lease = Acquire();
            Span<byte> message = stackalloc byte[Errors.BufferLength];
            int phase = NativeMethods.AbstractState_phase(
                lease.Value, out CLong errcode, message, new CLong(message.Length));
            Errors.Check(errcode, message);
            return phase;
        }
    }

    // ---------------------------------------------------------------- outputs

    /// <summary>Evaluates an output by name, for example <c>"Dmolar"</c>.</summary>
    public double KeyedOutput(string parameter) => KeyedOutput(ResolveParam(parameter));

    /// <summary>Evaluates an output by resolved key.</summary>
    public double KeyedOutput(long parameter)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_keyed_output(
            lease.Value, new CLong((nint)parameter), out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>Evaluates an output on the saturated-liquid side.</summary>
    public double SaturatedLiquidKeyedOutput(string parameter)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_saturated_liquid_keyed_output(
            lease.Value, new CLong((nint)ResolveParam(parameter)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>Evaluates an output on the saturated-vapour side.</summary>
    public double SaturatedVaporKeyedOutput(string parameter)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_saturated_vapor_keyed_output(
            lease.Value, new CLong((nint)ResolveParam(parameter)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>
    /// Evaluates an output on a named saturated state, <c>"liquid"</c> or <c>"gas"</c>.
    /// </summary>
    public double KeyedOutputSatState(string saturatedState, string parameter)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_keyed_output_satState(
            lease.Value, saturatedState, new CLong((nint)ResolveParam(parameter)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    // ------------------------------------------------------------ derivatives

    /// <summary>First derivative along the saturation curve.</summary>
    public double FirstSaturationDeriv(string of, string wrt)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_first_saturation_deriv(
            lease.Value, new CLong((nint)ResolveParam(of)), new CLong((nint)ResolveParam(wrt)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>First partial derivative at constant <paramref name="constant"/>.</summary>
    public double FirstPartialDeriv(string of, string wrt, string constant)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_first_partial_deriv(
            lease.Value, new CLong((nint)ResolveParam(of)), new CLong((nint)ResolveParam(wrt)),
            new CLong((nint)ResolveParam(constant)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>Second partial derivative.</summary>
    public double SecondPartialDeriv(string of1, string wrt1, string constant1, string wrt2, string constant2)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_second_partial_deriv(
            lease.Value, new CLong((nint)ResolveParam(of1)), new CLong((nint)ResolveParam(wrt1)),
            new CLong((nint)ResolveParam(constant1)), new CLong((nint)ResolveParam(wrt2)),
            new CLong((nint)ResolveParam(constant2)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>First two-phase derivative.</summary>
    public double FirstTwoPhaseDeriv(string of, string wrt, string constant)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_first_two_phase_deriv(
            lease.Value, new CLong((nint)ResolveParam(of)), new CLong((nint)ResolveParam(wrt)),
            new CLong((nint)ResolveParam(constant)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>Second two-phase derivative.</summary>
    public double SecondTwoPhaseDeriv(string of1, string wrt1, string constant1, string wrt2, string constant2)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_second_two_phase_deriv(
            lease.Value, new CLong((nint)ResolveParam(of1)), new CLong((nint)ResolveParam(wrt1)),
            new CLong((nint)ResolveParam(constant1)), new CLong((nint)ResolveParam(wrt2)),
            new CLong((nint)ResolveParam(constant2)),
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>First two-phase derivative, splined towards <paramref name="xEnd"/>.</summary>
    public double FirstTwoPhaseDerivSplined(string of, string wrt, string constant, double xEnd)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_first_two_phase_deriv_splined(
            lease.Value, new CLong((nint)ResolveParam(of)), new CLong((nint)ResolveParam(wrt)),
            new CLong((nint)ResolveParam(constant)), xEnd,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    // --------------------------------------------------------------- mixtures

    /// <summary>The mole fractions currently set on the state.</summary>
    public double[] MoleFractions()
        => ReadVector((CLong h, Span<double> buf, CLong max, out CLong n,
                       out CLong err, Span<byte> msg, CLong msgLen)
            => NativeMethods.AbstractState_get_mole_fractions(h, buf, max, out n, out err, msg, msgLen));

    /// <summary>Fugacities of all components, Pa.</summary>
    public double[] Fugacities()
        => ReadVector((CLong h, Span<double> buf, CLong max, out CLong n,
                       out CLong err, Span<byte> msg, CLong msgLen)
            => NativeMethods.AbstractState_get_fugacities(h, buf, max, out n, out err, msg, msgLen));

    /// <summary>Fugacity coefficients of all components.</summary>
    public double[] FugacityCoefficients()
        => ReadVector((CLong h, Span<double> buf, CLong max, out CLong n,
                       out CLong err, Span<byte> msg, CLong msgLen)
            => NativeMethods.AbstractState_get_fugacity_coefficients(h, buf, max, out n, out err, msg, msgLen));

    /// <summary>Chemical potentials of all components, J/mol.</summary>
    public double[] ChemicalPotentials()
        => ReadVector((CLong h, Span<double> buf, CLong max, out CLong n,
                       out CLong err, Span<byte> msg, CLong msgLen)
            => NativeMethods.AbstractState_get_chemical_potentials(h, buf, max, out n, out err, msg, msgLen));

    /// <summary>Fugacity of a single component, Pa.</summary>
    public double Fugacity(int componentIndex)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        double value = NativeMethods.AbstractState_get_fugacity(
            lease.Value, new CLong(componentIndex), out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return value;
    }

    /// <summary>Sets a binary interaction parameter between two components.</summary>
    public void SetBinaryInteractionDouble(int i, int j, string parameter, double value)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_set_binary_interaction_double(
            lease.Value, new CLong(i), new CLong(j), parameter, value,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Sets a fluid-specific parameter on one component.</summary>
    public void SetFluidParameterDouble(int i, string parameter, double value)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_set_fluid_parameter_double(
            lease.Value, new CLong(i), parameter, value,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Sets the cubic alpha function coefficients on one component.</summary>
    public void SetCubicAlphaC(int i, string parameter, double c1, double c2, double c3)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_set_cubic_alpha_C(
            lease.Value, new CLong(i), parameter, c1, c2, c3,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    // -------------------------------------------------------------- envelopes

    /// <summary>Builds the phase envelope. Call before <see cref="GetPhaseEnvelope"/>.</summary>
    public void BuildPhaseEnvelope(string level = "")
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_build_phase_envelope(
            lease.Value, level, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>
    /// Reads the phase envelope built by <see cref="BuildPhaseEnvelope"/>.
    /// </summary>
    /// <remarks>
    /// Sized in one probe: the native routine writes <c>actual_length</c> before
    /// it rejects an undersized buffer, so a zero-length call reports the size
    /// required. The component count comes from the composition rather than a
    /// second probe.
    /// </remarks>
    public PhaseEnvelope GetPhaseEnvelope()
    {
        int components = MoleFractions().Length;

        int length = ProbeEnvelopeLength(components);
        if (length == 0)
        {
            return new PhaseEnvelope([], [], [], [], new double[0, 0], new double[0, 0]);
        }

        double[] t = new double[length];
        double[] p = new double[length];
        double[] rhoVap = new double[length];
        double[] rhoLiq = new double[length];
        double[] x = new double[length * components];
        double[] y = new double[length * components];

        using (Lease lease = Acquire())
        {
            Span<byte> message = stackalloc byte[Errors.BufferLength];
            NativeMethods.AbstractState_get_phase_envelope_data_checkedMemory(
                lease.Value, new CLong(length), new CLong(components),
                t, p, rhoVap, rhoLiq, x, y,
                out _, out _, out CLong errcode, message, new CLong(message.Length));
            Errors.Check(errcode, message);
        }

        var liquid = new double[length, components];
        var vapor = new double[length, components];
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < components; j++)
            {
                liquid[i, j] = x[(i * components) + j];
                vapor[i, j] = y[(i * components) + j];
            }
        }

        return new PhaseEnvelope(t, p, rhoVap, rhoLiq, liquid, vapor);
    }

    private int ProbeEnvelopeLength(int components)
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        Span<double> empty = [];

        NativeMethods.AbstractState_get_phase_envelope_data_checkedMemory(
            lease.Value, new CLong(0), new CLong(components),
            empty, empty, empty, empty, empty, empty,
            out CLong actualLength, out _, out CLong errcode, message, new CLong(message.Length));

        // An error here is expected whenever the envelope is non-empty — the
        // zero-length buffer is what triggers it — but actual_length is written
        // first. Only treat it as fatal if no length came back.
        if (errcode.Value != 0 && actualLength.Value <= 0)
        {
            Errors.Check(errcode, message);
        }

        return (int)actualLength.Value;
    }

    /// <summary>Builds the spinodal curve. Call before <see cref="GetSpinodalData"/>.</summary>
    public void BuildSpinodal()
    {
        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_build_spinodal(
            lease.Value, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
    }

    /// <summary>Reads the spinodal curve, given the number of points to read.</summary>
    public (double[] Tau, double[] Delta, double[] M1) GetSpinodalData(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        double[] tau = new double[length];
        double[] delta = new double[length];
        double[] m1 = new double[length];

        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_get_spinodal_data(
            lease.Value, new CLong(length), tau, delta, m1,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);

        return (tau, delta, m1);
    }

    /// <summary>Reads up to <paramref name="maxPoints"/> critical points.</summary>
    public CriticalPoint[] AllCriticalPoints(int maxPoints = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxPoints);

        double[] t = new double[maxPoints];
        double[] p = new double[maxPoints];
        double[] rho = new double[maxPoints];
        CLong[] stable = new CLong[maxPoints];

        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_all_critical_points(
            lease.Value, new CLong(maxPoints), t, p, rho, stable,
            out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);

        var points = new List<CriticalPoint>(maxPoints);
        for (int i = 0; i < maxPoints; i++)
        {
            // Unused slots come back as zeroes; a critical point at 0 K is not real.
            if (t[i] > 0)
            {
                points.Add(new CriticalPoint(t[i], p[i], rho[i], stable[i].Value != 0));
            }
        }

        return [.. points];
    }

    // -------------------------------------------------------------- metadata

    /// <summary>The backend backing this state, for example <c>"HEOS"</c>.</summary>
    public string BackendName()
    {
        using Lease lease = Acquire();
        Span<byte> buffer = stackalloc byte[Errors.BufferLength];
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_backend_name(
            lease.Value, buffer, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return Errors.DecodeMessage(buffer);
    }

    /// <summary>The component names, delimited by the configured list separator.</summary>
    public string FluidNames()
    {
        using Lease lease = Acquire();
        Span<byte> buffer = stackalloc byte[Errors.BufferLength];
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_fluid_names(
            lease.Value, buffer, out CLong errcode, message, new CLong(message.Length));
        Errors.Check(errcode, message);
        return Errors.DecodeMessage(buffer);
    }

    // --------------------------------------------------------------- helpers

    private delegate void VectorGetter(
        CLong handle, Span<double> values, CLong maxN, out CLong n,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    /// <summary>
    /// Runs a <c>maxN</c>-in / <c>N</c>-out getter, growing once if the initial
    /// buffer was too small. The native routine writes <c>N</c> before rejecting
    /// an undersized buffer, so the retry is exact rather than a guess.
    /// </summary>
    private double[] ReadVector(VectorGetter getter)
    {
        const int InitialCapacity = 32;

        using Lease lease = Acquire();
        Span<byte> message = stackalloc byte[Errors.BufferLength];

        Span<double> probe = stackalloc double[InitialCapacity];
        getter(lease.Value, probe, new CLong(InitialCapacity), out CLong n,
               out CLong errcode, message, new CLong(message.Length));

        if (errcode.Value == 0)
        {
            return probe[..(int)n.Value].ToArray();
        }

        // Only a capacity shortfall is retryable; anything else is a real error.
        if (n.Value <= InitialCapacity)
        {
            Errors.Check(errcode, message);
        }

        double[] values = new double[n.Value];
        getter(lease.Value, values, new CLong(values.Length), out CLong n2,
               out CLong errcode2, message, new CLong(message.Length));
        Errors.Check(errcode2, message);

        return values[..(int)n2.Value];
    }

    private static long ResolveParam(string parameter)
    {
        long key = Information.GetParamIndex(parameter);
        return key < 0
            ? throw new CoolPropException($"Unknown CoolProp parameter '{parameter}'.")
            : key;
    }

    private static long ResolveInputPair(string inputPair)
    {
        long key = Information.GetInputPairIndex(inputPair);
        return key < 0
            ? throw new CoolPropException($"Unknown CoolProp input pair '{inputPair}'.")
            : key;
    }
}
