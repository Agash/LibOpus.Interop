using System.Runtime.InteropServices;
using Opus.Interop.Native;

namespace Opus.Interop;

/// <summary>A call into libopus failed; <see cref="Error"/> says how.</summary>
public sealed class OpusException : Exception
{
    /// <summary>An exception for a libopus error code.</summary>
    /// <param name="error">The code.</param>
    public OpusException(OpusError error)
        : base(Describe(error)) => Error = error;

    /// <summary>An exception with a message of its own.</summary>
    public OpusException()
        : base("libopus failed.") { }

    /// <summary>An exception with a message of its own.</summary>
    /// <param name="message">The message.</param>
    public OpusException(string message)
        : base(message) { }

    /// <summary>An exception with a message of its own and its cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public OpusException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The libopus error code.</summary>
    public OpusError Error { get; } = OpusError.InternalError;

    // A libopus result: the value itself when not negative, an exception otherwise.
    internal static int ThrowIfError(int result) =>
        result < 0 ? throw new OpusException((OpusError)result) : result;

    private static unsafe string Describe(OpusError error) =>
        $"libopus: {Marshal.PtrToStringUTF8((nint)LibOpus.opus_strerror((int)error))} ({error}).";
}
