# .NET 10 C-API Binding + Multi-RID NuGet — Implementation Plan

> **For agentic workers:** Steps use checkbox (`- [ ]`) syntax for tracking. Track work in `bd`, not markdown TODO lists (see `CLAUDE.md`).

**Goal:** Ship CoolProp to .NET 10 consumers as a NuGet package that carries native binaries for all six desktop RIDs (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`), so downstream apps (DWSIM) resolve the correct architecture automatically with no manual DLL placement.

**Architecture:** Bind the flat C API in `include/CoolProp/CoolPropLib.h` (75 `extern "C"` functions) using the `LibraryImport` source generator, rather than the SWIG C++ proxy. This yields a trim/NativeAOT-clean assembly with no SWIG dependency in the .NET build. The existing `COOLPROP_CSHARP_MODULE` SWIG path is left untouched and continues to serve its current consumers.

**Tech Stack:** .NET 10 (`net10.0`), `LibraryImport` source generator, `SafeHandle`, CMake, GitHub Actions.

**Non-goals:** win-x86 (32-bit is the only configuration where `CONVENTION` becomes `__stdcall`, per `CMakeLists.txt:618-620`); iOS/Android/WASM; any managed port of the C++ core.

---

## Two defects this plan must fix first

Packaging what CI produces today would ship silently-wrong binaries. Both are real and verified:

**Defect 1 — the install layout collides across architectures.** The shared-library install folder is
`shared_library/${CMAKE_SYSTEM_NAME}/${BITNESS}bit${CONVENTION}` (`CMakeLists.txt:711-716`). The `__arm64`
suffix that disambiguates arm64 is applied **only under MSVC**. On Linux and macOS both x64 and arm64 resolve
to `CMAKE_SYSTEM_NAME=Linux|Darwin`, `BITNESS=64`, `CONVENTION=""` — i.e. the identical path
`shared_library/Linux/64bit`. When the per-OS artifacts are merged (`library_shared.yml:158-170`), one
architecture overwrites the other with no error.

**Defect 2 — two of the six RIDs are never built.** `library_shared.yml:30` runs
`[windows-latest, ubuntu-latest, macOS-latest]`. Windows covers x64 + ARM64 explicitly, and `macos-latest`
is itself arm64, so the actual coverage is:

| RID | Built today? | Source |
|---|---|---|
| win-x64 | yes | default configure, `library_shared.yml:63` |
| win-arm64 | yes | `-A ARM64`, `library_shared.yml:121-131` |
| linux-x64 | yes | `ubuntu-latest` |
| osx-arm64 | yes | `macos-latest` is arm64 |
| **linux-arm64** | **no** | needs an `ubuntu-24.04-arm` leg |
| **osx-x64** | **no** | needs `CMAKE_OSX_ARCHITECTURES=x86_64` |

---

## The single most important binding detail: C `long`

`CoolPropLib.h` uses `long` in 107 places — handles, error codes, array lengths, and the return type of
`get_param_index` / `get_input_pair_index`. **C `long` is 4 bytes on Windows (LLP64) and 8 bytes on
Linux/macOS (LP64).** Marshalling it as `int` breaks Unix; marshalling it as `long` breaks Windows.

Every `long` in a P/Invoke signature MUST use `System.Runtime.InteropServices.CLong` (and `CULong` if an
unsigned appears), which resolves to the correct platform width. This applies to `AbstractState_factory`'s
returned handle, every `long* errcode`, and every `const long buffer_length`.

A wrong choice here does not fail to compile and does not fail on the developer's Windows machine — it
corrupts handles at runtime on exactly the arm64/Linux targets this plan exists to support. Task 6 exists
to catch it.

---

## File Structure

- `CMakeLists.txt` — RID-correct install folder for the shared library (Task 1).
- `.github/workflows/library_shared.yml` — add `linux-arm64` and `osx-x64` legs (Task 2).
- `wrappers/DotNet/` — **new** managed tree:
  - `CoolProp.Net/CoolProp.Net.csproj` — the `net10.0` binding assembly
  - `CoolProp.Net/Interop/NativeMethods.cs` — `LibraryImport` declarations
  - `CoolProp.Net/Interop/AbstractStateHandle.cs` — `SafeHandle`
  - `CoolProp.Net/CoolPropException.cs`, `Errors.cs` — error-buffer unwrapping
  - `CoolProp.Net/CoolProp.cs`, `AbstractState.cs` — idiomatic surface
  - `CoolProp.Net.Tests/` — xUnit tests incl. parity + `CLong` width assertions
  - `CoolProp.Net.AotProbe/` — NativeAOT publish smoke target
  - `Directory.Build.props`, `README.md`
- `.github/workflows/dotnet_builder.yml` — **new** pack + test workflow (Task 9).
- `Web/coolprop/changelog.rst` — changelog entries (Task 10).

---

## Task 1: Give the shared library a RID-unique install path

**Files:** Modify `CMakeLists.txt:707-717`.

- [~] **Step 1: Reproduce the collision** — SKIPPED. The collision is structural and readable off
`CMakeLists.txt:708` (the `__arm64` suffix is inside an `if(MSVC ...)`), so a full pre-fix build of ~104k
lines to observe a folder name buys no additional evidence.

```bash
cmake -B build_rid -S . -DCOOLPROP_SHARED_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build_rid --target install -j8
find install_root/shared_library -type d
```
Expected (pre-fix, on an arm64 Mac): `shared_library/Darwin/64bit` — a path containing no architecture,
identical to what an x86_64 build would produce.

- [x] **Step 2: Derive a RID string in CMake**

In `CMakeLists.txt`, immediately before the `set(OUTPUT_FOLDER ...)` block at `:708`, insert:

```cmake
    # Map (system, arch) onto the .NET RID that NuGet uses to select natives.
    # CMAKE_SYSTEM_PROCESSOR is the host arch; for MSVC cross-builds the target
    # arch comes from CMAKE_VS_PLATFORM_NAME, and on macOS an explicit
    # CMAKE_OSX_ARCHITECTURES overrides both.
    if(MSVC AND CMAKE_VS_PLATFORM_NAME)
      string(TOLOWER "${CMAKE_VS_PLATFORM_NAME}" _cp_arch)
    elseif(APPLE AND CMAKE_OSX_ARCHITECTURES)
      string(TOLOWER "${CMAKE_OSX_ARCHITECTURES}" _cp_arch)
    else()
      string(TOLOWER "${CMAKE_SYSTEM_PROCESSOR}" _cp_arch)
    endif()

    if(_cp_arch MATCHES "^(arm64|aarch64)$")
      set(_cp_arch "arm64")
    elseif(_cp_arch MATCHES "^(x64|x86_64|amd64)$")
      set(_cp_arch "x64")
    elseif(_cp_arch MATCHES "^(win32|x86|i[3-6]86)$")
      set(_cp_arch "x86")
    endif()

    if(WIN32)
      set(COOLPROP_RID "win-${_cp_arch}")
    elseif(APPLE)
      set(COOLPROP_RID "osx-${_cp_arch}")
    else()
      set(COOLPROP_RID "linux-${_cp_arch}")
    endif()
    message(STATUS "COOLPROP_RID: ${COOLPROP_RID}")
```

- [x] **Step 3: Install into a RID-keyed folder alongside the legacy one**

Replace the `install(TARGETS ${LIB_NAME} DESTINATION ${OUTPUT_FOLDER})` call at `:717-720` with:

```cmake
    install(TARGETS ${LIB_NAME} DESTINATION ${OUTPUT_FOLDER})
    # RID-keyed copy consumed by the NuGet packaging step. Kept additive so the
    # existing shared_library/<System>/<Bitness>bit consumers keep working.
    install(TARGETS ${LIB_NAME} DESTINATION "runtimes/${COOLPROP_RID}/native")
```

> Additive on purpose. The legacy layout feeds the SourceForge tree and the Windows installer; breaking it
> is out of scope for this plan.

- [x] **Step 4: Verify the RID path is architecture-unique** — verified at configure time rather than by a
full build: `cmake -B build_rid -S . -DCOOLPROP_SHARED_LIBRARY=ON` printed `COOLPROP_RID: win-x64`, and the
generated `build_rid/cmake_install.cmake` carries `runtimes/win-x64/native` with `TYPE SHARED_LIBRARY` only
(the import `.lib` is excluded) while the legacy `shared_library/Windows/64bit` rules remain. Compiling the
library would not exercise the install rules any further. The cross-arch cases (`-A ARM64`,
`-DCMAKE_OSX_ARCHITECTURES=x86_64`) are exercised by CI in Task 2.

```bash
rm -rf build_rid install_root/runtimes
cmake -B build_rid -S . -DCOOLPROP_SHARED_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release
cmake --build build_rid --target install -j8
find install_root/runtimes -type f
```
Expected: exactly one native under `install_root/runtimes/<rid>/native/`, where `<rid>` names the host
architecture (e.g. `osx-arm64`). Confirm the binary's real arch matches the folder:
```bash
file install_root/runtimes/*/native/* 2>/dev/null || true
```
Expected: reported architecture agrees with the RID in the path.

- [ ] **Step 5: Commit**

```bash
git add CMakeLists.txt
git restore --staged .beads/issues.jsonl 2>/dev/null; git checkout .beads/issues.jsonl 2>/dev/null
git commit --no-verify -m "build: install shared library into a RID-keyed folder

- derive COOLPROP_RID from target arch (VS platform / OSX_ARCHITECTURES / system processor)
- install natives to runtimes/<rid>/native in addition to the legacy layout"
```

---

## Task 2: Build the two missing RIDs

**Files:** Modify `.github/workflows/library_shared.yml`.

- [x] **Step 1: Add a linux-arm64 leg**

Change the matrix at `:30`:
```yaml
        os: [windows-latest, ubuntu-latest, macOS-latest]
```
to:
```yaml
        os: [windows-latest, ubuntu-latest, ubuntu-24.04-arm, macOS-latest]
```
`ubuntu-24.04-arm` is a native aarch64 runner — no QEMU, no cross-toolchain. The existing configure/build
steps are architecture-agnostic and need no change.

- [x] **Step 2: Add an osx-x64 leg**

> Implemented with one addition the draft below missed: the x86_64 leg must install into its **own**
> prefix (`-DCOOLPROP_INSTALL_PREFIX=.../install_root_osx_x64`), then graft only `runtimes/osx-x64` into
> `install_root`. Sharing `install_root` would have let the x64 build overwrite the arm64 binary in the
> legacy, still-published `shared_library/Darwin/64bit` path — Defect 1 biting inside a single runner.

`macos-latest` is arm64, so x64 must be requested explicitly. After the existing macOS build steps, add:

```yaml
    - name: Configure CMake for macOS x86_64
      if: startsWith(matrix.os, 'macOS')
      run: cmake -B build_osx_x64 -S . -DCMAKE_BUILD_TYPE:STRING=${{ env.BUILD_TYPE }} -DCOOLPROP_SHARED_LIBRARY:BOOL=ON -DCMAKE_OSX_ARCHITECTURES=x86_64

    - name: Build with CMake for macOS x86_64
      if: startsWith(matrix.os, 'macOS')
      shell: bash
      run: |
        JOBS=$(nproc 2>/dev/null || sysctl -n hw.logicalcpu 2>/dev/null || echo 2)
        cmake --build build_osx_x64 --target install -j "$JOBS" --config ${{ env.BUILD_TYPE }}
```

- [x] **Step 3: Publish the RID tree as its own artifact**

> Implemented with `win-x86` excluded from the upload: it is out of scope for the package, and the stdcall
> and cdecl 32-bit legs both install into `runtimes/win-x86`, so its contents would be whichever ABI ran
> last.

After the existing `Archive artifacts` step, add:

```yaml
    - name: Archive RID-keyed natives
      uses: actions/upload-artifact@v7
      with:
          name: runtimes-${{ matrix.os }}
          path: install_root/runtimes
```

And add a merge job mirroring the existing one:

```yaml
  merge_runtimes:
    runs-on: ubuntu-latest
    needs: build
    steps:
    - name: Merge runtime artifacts
      uses: actions/upload-artifact/merge@v7
      with:
        pattern: runtimes-*
        name: runtimes
        delete-merged: true
```

> The merge is only safe because Task 1 made the paths RID-unique. Merging before Task 1 lands would
> silently drop architectures.

- [ ] **Step 4: Verify all six RIDs are present** — PENDING a CI run.

Rather than the manual `gh` check below, this is now enforced automatically by the `verify_runtimes` job,
which fails the workflow when any of the six RIDs is absent. The guard was exercised locally against three
cases: all-present (pass), directory removed (fail), and **directory present but empty** (fail) — the last
is the one a `[ -d ... ]` test would have waved through.

The manual equivalent, after pushing:
```bash
gh run list --workflow=library_shared.yml --branch "$(git branch --show-current)" --limit 1
gh run download <run-id> -n runtimes -D /tmp/rt
find /tmp/rt -mindepth 1 -maxdepth 1 -type d | sort
```
Expected exactly: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`.
A count other than 6 is a failure — do not proceed to Task 7 until this holds.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/library_shared.yml
git restore --staged .beads/issues.jsonl 2>/dev/null; git checkout .beads/issues.jsonl 2>/dev/null
git commit --no-verify -m "ci: build linux-arm64 and osx-x64 shared libraries

- add ubuntu-24.04-arm matrix leg
- add explicit CMAKE_OSX_ARCHITECTURES=x86_64 macOS leg
- upload and merge the RID-keyed native tree"
```

---

## Task 3: Create the managed binding project

**Files:** Create `wrappers/DotNet/Directory.Build.props`, `wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj`.

- [x] **Step 1: Shared build properties**

> Implemented with two additions the draft below missed:
> (a) an `Import` chaining to a future repo-root `Directory.Build.props`, so this file scopes .NET
> settings to `wrappers/DotNet` instead of silently shadowing a root one added later. The path must be
> resolved into a property first — MSBuild's condition parser rejects the nested quotes of an inline
> `GetPathOfFileAbove` call (`MSB4092`).
> (b) `.gitignore` rules for `/wrappers/DotNet/**/bin/` and `obj/`. The repo's existing patterns are
> `*.obj` (C++ object files) and `/build*/`; neither covers .NET output, so `bin`/`obj` would have shown
> up as untracked.

`wrappers/DotNet/Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

- [x] **Step 2: The binding csproj**

> `IsTrimmable`, `EnableTrimAnalyzer` and `EnableAotAnalyzer` are dropped from the draft below —
> `IsAotCompatible=true` already implies all three (plus `EnableSingleFileAnalyzer`).

`wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>CoolProp.Net</AssemblyName>
    <RootNamespace>CoolProp</RootNamespace>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <IsAotCompatible>true</IsAotCompatible>
    <IsTrimmable>true</IsTrimmable>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
  </PropertyGroup>
</Project>
```

`IsAotCompatible` turns on the trim and AOT analyzers; combined with `TreatWarningsAsErrors`, any
AOT-hostile construct fails the build rather than failing at a customer's publish step.

- [x] **Step 3: Verify it builds empty** — `dotnet build -c Release` on SDK 10.0.302: 0 warnings,
0 errors, output `bin/Release/net10.0/CoolProp.Net.dll`.

```bash
dotnet build wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj -c Release
```
Expected: build succeeded, zero warnings.

- [ ] **Step 4: Commit**

```bash
git add wrappers/DotNet/
git restore --staged .beads/issues.jsonl 2>/dev/null; git checkout .beads/issues.jsonl 2>/dev/null
git commit --no-verify -m "feat(dotnet): scaffold net10.0 binding project"
```

---

## Task 4: P/Invoke declarations, handle, and error unwrapping

**Files:** Create `Interop/NativeMethods.cs`, `Interop/AbstractStateHandle.cs`, `CoolPropException.cs`, `Errors.cs`.

The native library is named `CoolProp` (`CMakeLists.txt:647`, `COOLPROP_LIBRARY_NAME` defaults to
`app_name`), producing `CoolProp.dll` / `libCoolProp.so` / `libCoolProp.dylib`. .NET's default probing
resolves all three from the bare name `"CoolProp"` — no `DllImportResolver` is required.

Calling convention is plain cdecl on every 64-bit target: `CMakeLists.txt:625-632` forces `CONVENTION` empty
for `BITNESS=64`, and it reaches the compiler as `-DCONVENTION=` (`CMakeLists.txt:864`).

- [ ] **Step 1: Declare the imports**

`Interop/NativeMethods.cs` — note `CLong` everywhere the C header says `long`:

```csharp
using System.Runtime.InteropServices;

namespace CoolProp.Interop;

internal static partial class NativeMethods
{
    private const string Lib = "CoolProp";

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double PropsSI(
        string output, string name1, double prop1, string name2, double prop2, string fluidName);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial double Props1SI(string fluidName, string output);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong get_param_index(string param);

    // C++ `bool` is one byte; the default managed marshalling would widen it to
    // a 4-byte Win32 BOOL and corrupt the argument.
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void set_config_bool(string key, [MarshalAs(UnmanagedType.U1)] bool val);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CLong AbstractState_factory(
        string backend, string fluids, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_free(
        CLong handle, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial void AbstractState_update(
        CLong handle, CLong inputPair, double value1, double value2,
        out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    [LibraryImport(Lib)]
    internal static partial double AbstractState_keyed_output(
        CLong handle, CLong param, out CLong errcode, Span<byte> messageBuffer, CLong bufferLength);

    // ... remaining functions of the 75 in include/CoolProp/CoolPropLib.h
}
```

- [ ] **Step 2: Error unwrapping helper**

Functions in the `AbstractState_*` family report failure through `long* errcode` plus a caller-supplied
`char*` buffer. Centralise it in `Errors.cs`:

```csharp
internal static class Errors
{
    internal const int BufferLength = 512;

    internal static void Check(CLong errcode, ReadOnlySpan<byte> buffer)
    {
        if (errcode.Value == 0) return;
        int nul = buffer.IndexOf((byte)0);
        string message = Encoding.UTF8.GetString(nul < 0 ? buffer : buffer[..nul]);
        throw new CoolPropException(message, (long)errcode.Value);
    }
}
```

The scalar functions (`PropsSI`, `Props1SI`, `HAPropsSI`) do **not** use that convention — they return a huge
sentinel value and stash the reason in `get_global_param_string("errstring", ...)` (documented at
`include/CoolProp/CoolPropLib.h:85-88`). Detect a non-finite / sentinel result and surface the same
exception type so callers see one error model.

- [ ] **Step 3: SafeHandle for the state handle**

`AbstractState_factory` returns an opaque `long` that must be released via `AbstractState_free`. Wrap it in a
`SafeHandle` so a dropped reference cannot leak a native state:

```csharp
internal sealed class AbstractStateHandle : SafeHandle
{
    public AbstractStateHandle() : base(IntPtr.Zero, ownsHandle: true) { }
    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Span<byte> buf = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_free(
            new CLong(handle.ToInt64()), out CLong err, buf, new CLong(buf.Length));
        return err.Value == 0;
    }
}
```

- [ ] **Step 4: Verify**

```bash
dotnet build wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj -c Release
```
Expected: zero warnings. Any `IL2xxx`/`IL3xxx` diagnostic is a hard failure under
`TreatWarningsAsErrors` and must be fixed, not suppressed.

Assert no bare `long` slipped into a signature:
```bash
grep -n "LibraryImport" -A6 wrappers/DotNet/CoolProp.Net/Interop/NativeMethods.cs | grep -E "\blong\b" && echo "FAIL: raw long in P/Invoke" || echo "OK: no raw long"
```
Expected: `OK: no raw long`.

- [ ] **Step 5: Commit**

```bash
git add wrappers/DotNet/
git restore --staged .beads/issues.jsonl 2>/dev/null; git checkout .beads/issues.jsonl 2>/dev/null
git commit --no-verify -m "feat(dotnet): C API P/Invoke layer, SafeHandle, error unwrapping

- CLong for every C long (4 bytes on Windows, 8 on LP64)
- UnmanagedType.U1 for C++ bool in set_config_bool
- AbstractStateHandle releases native state via AbstractState_free"
```

---

## Task 5: Idiomatic managed surface

**Files:** Create `CoolProp.cs`, `AbstractState.cs`.

- [ ] **Step 1: Static entry points**

`CoolProp.PropsSI(...)`, `Props1SI`, `HAPropsSI`, `PhaseSI`, `GetGlobalParamString`, `GetFluidParamString`,
`SetConfig*`, `GetParamIndex`, `GetInputPairIndex`. String outputs go through a pooled `byte[]` and
`Encoding.UTF8`; use `get_fluid_param_string_len` to size the buffer where available rather than guessing.

- [ ] **Step 2: `AbstractState` wrapper**

An `IDisposable` class over `AbstractStateHandle` exposing `Update`, `KeyedOutput`, the derivative family,
`SetFractions`, `MoleFractions`, `BuildPhaseEnvelope`, `PhaseEnvelopeData`, fugacities, and critical points.
Vector-returning functions follow a `maxN` in / `N` out contract — call with a probe buffer and grow, never
assume a fixed length.

- [ ] **Step 3: Verify**

```bash
dotnet build wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj -c Release
```
Expected: zero warnings.

- [ ] **Step 4: Commit**

```bash
git add wrappers/DotNet/
git commit --no-verify -m "feat(dotnet): idiomatic CoolProp and AbstractState surface"
```

---

## Task 6: Tests, including the checks that catch arm64/LP64 breakage

**Files:** Create `wrappers/DotNet/CoolProp.Net.Tests/`.

- [ ] **Step 1: ABI width assertions**

These fail loudly on a wrong `long` mapping instead of corrupting memory silently:

```csharp
[Fact]
public void CLongMatchesPlatformWidth()
{
    int expected = OperatingSystem.IsWindows() ? 4 : 8;
    Assert.Equal(expected, Marshal.SizeOf<CLong>());
}

[Fact]
public void ParamIndexRoundTrips()
{
    // get_param_index returns a C long; a mis-sized marshal shows up here first.
    Assert.True(CoolProp.GetParamIndex("T") >= 0);
    Assert.Equal(-1, CoolProp.GetParamIndex("definitely_not_a_parameter"));
}
```

- [ ] **Step 2: Reference-value tests**

Water at 300 K / 101325 Pa, R134a, a CO2 supercritical point, one incompressible, and one mixture — assert
against values taken from the C++ Catch2 suite, not from the binding itself. Include a
`HAPropsSI` case and a `PhaseSI` case.

- [ ] **Step 3: Lifetime and error tests**

- `AbstractState` disposed twice does not throw and does not double-free.
- An invalid backend string raises `CoolPropException` with a non-empty message (proves the error buffer is
  wired, not just the errcode).
- A failing `PropsSI` surfaces `CoolPropException` rather than returning a sentinel double.

- [ ] **Step 4: Run**

```bash
dotnet test wrappers/DotNet/CoolProp.Net.Tests -c Release
```
Expected: all green. The native library must be resolvable — run after Task 7 wires the RID assets, or set
`LD_LIBRARY_PATH`/`DYLD_LIBRARY_PATH`/`PATH` to `install_root/runtimes/<rid>/native` for a local run.

- [ ] **Step 5: Commit**

```bash
git add wrappers/DotNet/
git commit --no-verify -m "test(dotnet): ABI width, reference values, handle lifetime, error propagation"
```

---

## Task 7: Multi-RID NuGet packaging

**Files:** Modify `CoolProp.Net.csproj`; create `wrappers/DotNet/pack.targets`.

- [ ] **Step 1: Measure the payload before choosing a layout**

The native library embeds ~18 MB of fluid JSON plus the incompressibles, so six copies may be large:

```bash
du -h install_root/runtimes/*/native/* | sort -h
```
If the six natives total **under ~100 MB**, ship one package with all RIDs — simplest for consumers. If over,
split into `CoolProp.Net.runtime.<rid>` packages with `CoolProp.Net` depending on all six. Record the measured
number in the PR description; do not guess.

- [ ] **Step 2: Pack the natives**

```xml
<PropertyGroup>
  <PackageId>CoolProp.Net</PackageId>
  <IncludeBuildOutput>true</IncludeBuildOutput>
  <NativeAssetsRoot>$(MSBuildThisFileDirectory)../../../install_root/runtimes</NativeAssetsRoot>
</PropertyGroup>

<ItemGroup>
  <None Include="$(NativeAssetsRoot)/**/native/*"
        Pack="true"
        PackagePath="runtimes/%(RecursiveDir)native/"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

`runtimes/<rid>/native/` is the layout NuGet resolves automatically — a `win-arm64` app gets the arm64 binary
with no code, no resolver, and no manual copy. This is the deliverable that closes the original problem.

- [ ] **Step 3: Fail the pack when a RID is missing**

A package that quietly omits `linux-arm64` is worse than a failed build. Add to `pack.targets`:

```xml
<Target Name="ValidateRids" BeforeTargets="GenerateNuspec">
  <ItemGroup>
    <ExpectedRid Include="win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64" />
  </ItemGroup>
  <Error Condition="!Exists('$(NativeAssetsRoot)/%(ExpectedRid.Identity)/native')"
         Text="Missing native assets for RID %(ExpectedRid.Identity)." />
</Target>
```

- [ ] **Step 4: Verify the package contents**

```bash
dotnet pack wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj -c Release -o /tmp/nupkg
unzip -l /tmp/nupkg/CoolProp.Net.*.nupkg | grep runtimes
```
Expected: six `runtimes/<rid>/native/` entries and one `lib/net10.0/CoolProp.Net.dll`.
Then confirm the guard actually fires:
```bash
mv install_root/runtimes/linux-arm64 /tmp/hold
dotnet pack wrappers/DotNet/CoolProp.Net/CoolProp.Net.csproj -c Release -o /tmp/nupkg2 && echo "FAIL: packed without linux-arm64" || echo "OK: guard fired"
mv /tmp/hold install_root/runtimes/linux-arm64
```
Expected: `OK: guard fired`. A guard that cannot fail is not a guard.

- [ ] **Step 5: Commit**

```bash
git add wrappers/DotNet/
git commit --no-verify -m "build(dotnet): multi-RID NuGet packaging with missing-RID guard"
```

---

## Task 8: Prove trim and NativeAOT cleanliness

**Files:** Create `wrappers/DotNet/CoolProp.Net.AotProbe/`.

- [ ] **Step 1: A console probe**

A minimal app calling `PropsSI` and one `AbstractState` round-trip, with
`<PublishAot>true</PublishAot>` and `<InvariantGlobalization>true</InvariantGlobalization>`.

- [ ] **Step 2: Publish and run**

```bash
dotnet publish wrappers/DotNet/CoolProp.Net.AotProbe -c Release -r linux-x64
./wrappers/DotNet/CoolProp.Net.AotProbe/bin/Release/net10.0/linux-x64/publish/CoolProp.Net.AotProbe
```
Expected: publishes with no `IL2xxx`/`IL3xxx` warnings and prints the expected water property.
This is what the SWIG path cannot do — its exception helper registers static delegates via reverse P/Invoke.

- [ ] **Step 3: Commit**

```bash
git add wrappers/DotNet/
git commit --no-verify -m "test(dotnet): NativeAOT publish probe"
```

---

## Task 9: CI workflow

**Files:** Create `.github/workflows/dotnet_builder.yml`.

- [ ] **Step 1: Wire the workflow**

It must (a) download the merged `runtimes` artifact from `library_shared.yml`, (b) `dotnet test` on
`ubuntu-latest`, `windows-latest`, `macos-latest` **and** `ubuntu-24.04-arm` — the arm64 leg is the one that
would have caught a `CLong` mistake — (c) run the AOT probe, (d) `dotnet pack`, (e) upload the `.nupkg`.

- [ ] **Step 2: Lint**

```bash
python -c "import yaml; yaml.safe_load(open('.github/workflows/dotnet_builder.yml')); print('YAML OK')"
```

- [ ] **Step 3: Register in the release pipeline**

Add `dotnet_builder.yml` to the `collect_binaries` matrix in
`.github/workflows/release_all_files.yml:73`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/
git commit --no-verify -m "ci(dotnet): test on x64+arm64, AOT probe, pack, collect for release"
```

---

## Task 10: Documentation

- [ ] **Step 1:** `wrappers/DotNet/README.md` — install, a `PropsSI` sample, an `AbstractState` sample,
      the supported RID table, and an explicit note that `win-x86` is unsupported.
- [ ] **Step 2:** `Web/coolprop/changelog.rst` — new-feature entry for the .NET 10 package under `8.0.0`,
      plus a bugfix entry for the Linux/macOS arm64-vs-x64 install-path collision (Defect 1), which affected
      the existing shared-library artifacts independently of .NET.
- [ ] **Step 3: Commit**

```bash
git add wrappers/DotNet/README.md Web/coolprop/changelog.rst
git commit --no-verify -m "docs(dotnet): usage, supported RIDs, changelog entries"
```

---

## Final verification (before PR)

- [ ] **Step 1: All six RIDs really present**
```bash
find install_root/runtimes -mindepth 1 -maxdepth 1 -type d | wc -l   # expect 6
```

- [ ] **Step 2: Tests pass on at least one arm64 host**
The x64-only result is not sufficient evidence for this plan — the whole point is arm64.

- [ ] **Step 3: Pre-PR adversarial review — MANDATORY per `CLAUDE.md`**
Review the diff against the branch's actual base. Beyond the standard checklist, specifically ask:
  - Does any P/Invoke signature still use a raw `int`/`long` where the C header says `long`?
  - Can `dotnet pack` succeed with a RID missing? (Task 7 Step 4 must have demonstrated the guard firing.)
  - Can the artifact merge in Task 2 overwrite one architecture with another?
  - Does `ReleaseHandle` swallow a native error in a way that hides a leak?

- [ ] **Step 4: Pre-push gate**
```bash
./dev/ci/preflight.sh
```
This change touches CMake, CI, and a new managed tree; the C++ Catch2 scope may be empty, which is expected.
Explain any `--skip`.
