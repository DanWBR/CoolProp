namespace CoolProp;

/// <summary>
/// Raised when a CoolProp native call reports failure.
/// </summary>
public sealed class CoolPropException : Exception
{
    /// <summary>
    /// The <c>errcode</c> reported by the native call, or 0 when the failure was
    /// signalled by a sentinel return value rather than an error code.
    /// </summary>
    public long ErrorCode { get; }

    public CoolPropException(string message)
        : base(message) => ErrorCode = 0;

    public CoolPropException(string message, long errorCode)
        : base(message) => ErrorCode = errorCode;

    public CoolPropException(string message, Exception innerException)
        : base(message, innerException) => ErrorCode = 0;
}
