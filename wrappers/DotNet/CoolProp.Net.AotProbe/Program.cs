using System.Runtime.InteropServices;
using CoolProp;

// Exercises the binding from a NativeAOT-published executable: no JIT, no
// reflection fallback, trimmed. A marshalling path that only works under the
// JIT fails here.
//
// Exit code 0 = every probe agreed with its reference value.

int failures = 0;

void Expect(string label, double actual, double expected, double tolerance)
{
    bool ok = Math.Abs(actual - expected) <= tolerance;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {label,-34} = {actual:G10}   (expected {expected:G10})");
    if (!ok)
    {
        failures++;
    }
}

void Assert(string label, bool condition, string detail)
{
    Console.WriteLine($"{(condition ? "ok  " : "FAIL")} {label,-34} = {detail}");
    if (!condition)
    {
        failures++;
    }
}

Console.WriteLine($"NativeAOT probe | RID {RuntimeInformation.RuntimeIdentifier} | CLong {Marshal.SizeOf<CLong>()} bytes");
Console.WriteLine();

try
{
    Assert("native version", Information.GetGlobalParamString("version").Length > 0,
        Information.GetGlobalParamString("version"));

    // String marshalling in, double out.
    Expect("water Tsat @ 1 atm", Props.PropsSI("T", "P", 101325, "Q", 0, "Water"), 373.1243, 1e-3);

    // IAPWS-95 defining constant.
    Expect("water Tcrit", Props.Props1SI("Water", "Tcrit"), 647.096, 1e-3);

    // Humid air: a separate native entry point.
    Expect("humid air enthalpy", Props.HAPropsSI("H", "T", 298.15, "P", 101325, "R", 0.5), 50423, 500);

    // CLong return path: an unknown name must come back as exactly -1.
    Assert("unknown param index", Information.GetParamIndex("not_a_parameter") == -1, "-1");

    // Handle lifetime through a SafeHandle, which AOT must not have broken.
    using (AbstractState state = AbstractState.Create("HEOS", "Water"))
    {
        state.Update("PT_INPUTS", 101325, 300);
        Assert("AbstractState density", state.KeyedOutput("Dmolar") > 0,
            state.KeyedOutput("Dmolar").ToString("G10"));
    }

    // Vector getter over a mixture.
    using (AbstractState mixture = AbstractState.Create("HEOS", "Methane&Ethane"))
    {
        mixture.SetFractions([0.7, 0.3]);
        double[] z = mixture.MoleFractions();
        Assert("mixture composition", z is [0.7, 0.3], $"[{string.Join(", ", z)}]");
    }

    // Error propagation: the message must survive trimming, not come back empty.
    try
    {
        using AbstractState bad = AbstractState.Create("NOT_A_BACKEND", "Water");
        Assert("invalid backend throws", false, "no exception");
    }
    catch (CoolPropException ex)
    {
        Assert("invalid backend throws", ex.Message.Contains("NOT_A_BACKEND"), ex.Message);
    }
}
catch (DllNotFoundException)
{
    Console.Error.WriteLine(
        $"The CoolProp native library is missing for RID {RuntimeInformation.RuntimeIdentifier}. " +
        "Build it with the CMake install target, or unpack the 'runtimes' artifact at the repository root.");
    return 2;
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "AOT PROBE PASSED" : $"AOT PROBE FAILED ({failures})");
return failures == 0 ? 0 : 1;
