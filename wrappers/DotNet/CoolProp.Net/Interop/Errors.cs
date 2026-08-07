using System.Runtime.InteropServices;
using System.Text;

namespace CoolProp.Interop;

/// <summary>
/// Translates the two native failure conventions into <see cref="CoolPropException"/>.
/// </summary>
internal static class Errors
{
    /// <summary>Size of the message buffer handed to the native error convention.</summary>
    internal const int BufferLength = 512;

    /// <summary>
    /// The sentinel the scalar entry points return on failure. CoolProp documents
    /// it as "a huge value"; <c>_HUGE</c> is <c>std::numeric_limits&lt;double&gt;::infinity()</c>,
    /// but a failed solve can also yield NaN, so both are treated as failure.
    /// </summary>
    internal static bool IsFailureSentinel(double value) => !double.IsFinite(value);

    /// <summary>
    /// Convention used by the <c>AbstractState_*</c> family: an <c>errcode</c>
    /// out-parameter plus a caller-supplied message buffer.
    /// </summary>
    internal static void Check(CLong errcode, ReadOnlySpan<byte> messageBuffer)
    {
        if (errcode.Value == 0)
        {
            return;
        }

        throw new CoolPropException(DecodeMessage(messageBuffer), errcode.Value);
    }

    /// <summary>
    /// Convention used by the scalar entry points (<c>PropsSI</c>, <c>HAPropsSI</c>, …):
    /// a non-finite return with the reason parked in the global error string.
    /// </summary>
    internal static double CheckScalar(double value, string context)
    {
        if (!IsFailureSentinel(value))
        {
            return value;
        }

        throw new CoolPropException(
            GetGlobalErrorStringOr($"{context} failed and reported no error message."));
    }

    /// <summary>
    /// The global error string, or <paramref name="fallback"/> when the native
    /// side left it empty.
    /// </summary>
    internal static string GetGlobalErrorStringOr(string fallback)
    {
        string reason = GetGlobalErrorString();
        return reason.Length == 0 ? fallback : reason;
    }

    /// <summary>
    /// Reads <c>get_global_param_string("errstring", …)</c>, the out-of-band slot
    /// where the scalar entry points leave their failure reason.
    /// </summary>
    private static string GetGlobalErrorString()
    {
        Span<byte> buffer = stackalloc byte[BufferLength];
        CLong rc = NativeMethods.get_global_param_string("errstring", buffer, buffer.Length);

        // A failure to read the error string must not mask the original failure.
        return rc.Value == 0 ? string.Empty : DecodeMessage(buffer);
    }

    /// <summary>
    /// Decodes a NUL-terminated UTF-8 buffer. A buffer the native side filled
    /// completely has no terminator, so fall back to the whole span.
    /// </summary>
    internal static string DecodeMessage(ReadOnlySpan<byte> buffer)
    {
        int nul = buffer.IndexOf((byte)0);
        return Encoding.UTF8.GetString(nul < 0 ? buffer : buffer[..nul]);
    }
}
