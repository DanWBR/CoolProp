using System.Runtime.InteropServices;

namespace CoolProp.Interop;

/// <summary>
/// Owns an <c>AbstractState</c> slot in the native handle table and releases it
/// through <c>AbstractState_free</c>.
/// </summary>
/// <remarks>
/// The native handle is an index into a <c>std::map</c>, not a pointer. Valid
/// indices start at <b>0</b> and <c>AbstractState_factory</c> returns <b>-1</b>
/// on failure (see <c>src/CoolPropLib.cpp</c>, <c>AbstractStateLibrary::add</c>).
/// The invalid sentinel is therefore -1: treating 0 as invalid would leak the
/// first state created in the process, because <see cref="SafeHandle"/> never
/// calls <see cref="ReleaseHandle"/> on a handle it considers invalid.
/// </remarks>
internal sealed class AbstractStateHandle : SafeHandle
{
    private static readonly IntPtr InvalidValue = new(-1);

    private AbstractStateHandle()
        : base(InvalidValue, ownsHandle: true)
    {
    }

    public override bool IsInvalid => handle == InvalidValue;

    /// <summary>
    /// Wraps a handle already returned by <c>AbstractState_factory</c>.
    /// </summary>
    internal static AbstractStateHandle FromNative(CLong nativeHandle)
    {
        var wrapper = new AbstractStateHandle();
        wrapper.SetHandle(nativeHandle.Value);
        return wrapper;
    }

    /// <summary>
    /// The raw table index, for passing to the native entry points. Only valid
    /// while a reference to this object is held; callers must keep the handle
    /// alive across the native call (the public wrapper takes a ref count).
    /// </summary>
    internal CLong DangerousGetCLong() => new(handle);

    protected override bool ReleaseHandle()
    {
        Span<byte> message = stackalloc byte[Errors.BufferLength];
        NativeMethods.AbstractState_free(
            new CLong(handle), out CLong errcode, message, new CLong(message.Length));

        // A false return surfaces as a ReleaseHandleFailed managed debugging
        // assistant rather than an exception; finalizers must not throw.
        return errcode.Value == 0;
    }
}
