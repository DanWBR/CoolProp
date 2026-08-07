# CoolProp.Net — .NET 10 bindings

A .NET binding for CoolProp that talks to the flat C API in
[`include/CoolProp/CoolPropLib.h`](../../include/CoolProp/CoolPropLib.h) through the
`LibraryImport` source generator. The NuGet package carries the native library for
every supported runtime, so a consuming application resolves the right architecture
with no manual DLL placement.

This is **additive**. The existing SWIG C# wrapper (`CoolPropCsharp`, built by
`-DCOOLPROP_CSHARP_MODULE=ON`) is unchanged and still supported.

## Install

```bash
dotnet add package CoolProp.Net
```

## Supported runtimes

| RID | Notes |
|---|---|
| `win-x64` | |
| `win-arm64` | cross-compiled with `cmake -A ARM64` |
| `linux-x64` | |
| `linux-arm64` | built natively on an aarch64 runner |
| `osx-x64` | built with `-DCMAKE_OSX_ARCHITECTURES=x86_64` |
| `osx-arm64` | |

**32-bit Windows (`win-x86`) is not supported.** It is the only configuration in
which the C API's `CONVENTION` macro becomes `__stdcall`
([`CMakeLists.txt`](../../CMakeLists.txt), the `BITNESS` block); every 64-bit target
is plain cdecl. Supporting it would need a conditional `CallingConvention`, and the
32-bit build additionally produces two mutually incompatible ABIs (stdcall and
cdecl) into a single output folder.

Mobile (iOS, Android) and WebAssembly are out of scope for this package.

## Usage

### One-shot property calls

```csharp
using CoolProp;

// Saturation temperature of water at 1 atm, in K
double tsat = Props.PropsSI("T", "P", 101325, "Q", 0, "Water");

// A property that needs no state inputs
double tcrit = Props.Props1SI("Water", "Tcrit");

// Humid air: enthalpy at 25 °C, 1 atm, 50 % relative humidity, J/kg dry air
double h = Props.HAPropsSI("H", "T", 298.15, "P", 101325, "R", 0.5);

// Phase name at a state
string phase = Props.PhaseSI("T", 300, "P", 101325, "Water");
```

### Reusing a state object

`AbstractState` avoids re-parsing the fluid string on every call and exposes the
full surface — derivatives, saturation states, phase envelopes, fugacities.

```csharp
using CoolProp;

using AbstractState state = AbstractState.Create("HEOS", "Water");
state.Update("PT_INPUTS", 101325, 300);

double rho = state.KeyedOutput("Dmolar");
double cp  = state.KeyedOutput("Cpmolar");
double dh  = state.FirstPartialDeriv("Hmolar", "T", "P");
```

### Mixtures

```csharp
using AbstractState mixture = AbstractState.Create("HEOS", "Methane&Ethane");
mixture.SetFractions([0.7, 0.3]);
mixture.Update("PT_INPUTS", 101325, 300);

double[] z = mixture.MoleFractions();
double[] phi = mixture.FugacityCoefficients();
```

### Error handling

Every failure surfaces as `CoolPropException`, including the scalar entry points
that natively signal failure by returning a non-finite sentinel. The binding never
hands back an infinity for a caller to propagate into a simulation.

```csharp
try
{
    double t = Props.PropsSI("T", "P", -1, "Q", 0, "Water");
}
catch (CoolPropException ex)
{
    // "T is not a valid number : PropsSI("T","P",-1,"Q",0,"Water")"
    Console.Error.WriteLine(ex.Message);
}
```

### Configuration and metadata

```csharp
Config.SetBool("DONT_CHECK_PROPERTY_LIMITS", true);
Config.SetReferenceState("Propane", "ASHRAE");

string version = Information.GetGlobalParamString("version");
bool ok = Information.IsValidFluidString("Water");
(string backend, string fluid) = Information.ExtractBackend("HEOS::Water");
```

## Trimming and NativeAOT

The assembly is marked `IsAotCompatible`, and `wrappers/DotNet/CoolProp.Net.AotProbe`
publishes with `PublishAot` and asserts reference values from the resulting native
executable. Because the binding uses `LibraryImport` rather than SWIG's generated
proxy — whose exception helper registers static delegates via reverse P/Invoke —
there is no reflection or delegate marshalling to break under trimming.

## Building from source

The managed projects build on their own; the native library is a separate step.

```bash
# 1. Build and install the native library for the current architecture
cmake -B build_rid -S . -DCOOLPROP_SHARED_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build_rid --config Release --target install

# 2. Build, test and pack the managed side
dotnet build wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj -c Release
dotnet test  wrappers/DotNet/CoolProp.Net.Tests -c Release
```

Step 1 installs to `install_root/runtimes/<rid>/native/`, which the test project and
the AOT probe copy into their output. A missing native library is deliberately not a
build error — the managed assemblies must still compile on a machine that has never
built the C++ side — so the tests report it as one readable failure instead.

`dotnet pack` requires **all six** natives to be present and refuses to produce a
package otherwise, since a package silently missing one architecture fails only on
the end user's machine. To pack, either build each architecture or unpack the
`runtimes` artifact from the `library_shared` workflow at the repository root.

### Windows: NativeAOT needs `vswhere` on `PATH`

Publishing with `PublishAot` shells out to `vswhere.exe` to locate the MSVC linker,
and it is not on `PATH` by default even with Visual Studio installed. Without it the
publish fails at the link step with `MSB3073 ... exited with code 123`:

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
```

GitHub's `windows-latest` runners already have it.

## Implementation notes

**Every C `long` is marshalled as `CLong`.** C `long` is 4 bytes on Windows (LLP64)
and 8 bytes on Linux and macOS (LP64). Binding it as `int` corrupts arguments on
Unix; binding it as `long` corrupts them on Windows. Neither mistake fails to
compile, and neither shows up on a Windows-only test run — it appears as corrupted
handles on exactly the arm64 and Linux targets this package exists to serve.
`AbiTests` asserts the width explicitly.

**Handle 0 is valid.** The native handle is an index into a `std::map` starting at
0, and `AbstractState_factory` returns -1 on failure. The `SafeHandle` therefore
uses -1 as its invalid sentinel; treating 0 as invalid would leak the first state
created in a process, because `SafeHandle` never releases a handle it considers
invalid.

**Return conventions are not uniform.** The string getters and
`set_reference_state*` return 1 on success and 0 on failure; `C_extract_backend`
returns 0 on success and -1 on failure; `C_is_valid_fluid_string` returns 1, 0, or
-1 when the check itself threw. Each is handled individually rather than assumed
symmetric.

**The Fortran shims are not bound.** `propssi_`, `hapropssi_` and `haprops_` exist
for Fortran's calling convention and have no use from .NET. The other 72 exports are
all bound.
