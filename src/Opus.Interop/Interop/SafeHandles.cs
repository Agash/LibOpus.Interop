using System.Runtime.InteropServices;
using Opus.Interop.Native;
using NativeDecoder = Opus.Interop.Native.OpusDecoder;
using NativeEncoder = Opus.Interop.Native.OpusEncoder;

namespace Opus.Interop.Interop;

// Owns an NativeEncoder*; destroys it when released.
internal sealed unsafe class SafeEncoderHandle() : SafeHandle(0, ownsHandle: true)
{
    public override bool IsInvalid => handle == 0;

    public NativeEncoder* Pointer => (NativeEncoder*)handle;

    public static SafeEncoderHandle Create(
        int sampleRate,
        int channels,
        OpusApplication application
    )
    {
        int error;
        NativeEncoder* encoder = LibOpus.opus_encoder_create(
            sampleRate,
            channels,
            (int)application,
            &error
        );
        _ = OpusException.ThrowIfError(error);
        SafeEncoderHandle owned = new();
        owned.SetHandle((nint)encoder);
        return owned;
    }

    protected override bool ReleaseHandle()
    {
        LibOpus.opus_encoder_destroy((NativeEncoder*)handle);
        return true;
    }
}

// Owns an NativeDecoder*; destroys it when released.
internal sealed unsafe class SafeDecoderHandle() : SafeHandle(0, ownsHandle: true)
{
    public override bool IsInvalid => handle == 0;

    public NativeDecoder* Pointer => (NativeDecoder*)handle;

    public static SafeDecoderHandle Create(int sampleRate, int channels)
    {
        int error;
        NativeDecoder* decoder = LibOpus.opus_decoder_create(sampleRate, channels, &error);
        _ = OpusException.ThrowIfError(error);
        SafeDecoderHandle owned = new();
        owned.SetHandle((nint)decoder);
        return owned;
    }

    protected override bool ReleaseHandle()
    {
        LibOpus.opus_decoder_destroy((NativeDecoder*)handle);
        return true;
    }
}
