using static Opus.Interop.Native.LibOpus;

namespace Opus.Interop;

/// <summary>What an encoder is tuned for, chosen when it is created (<c>OPUS_APPLICATION_*</c>).</summary>
public enum OpusApplication
{
    /// <summary>Speech intelligibility first: voice calls and conferencing.</summary>
    Voip = OPUS_APPLICATION_VOIP,

    /// <summary>Faithful reproduction of the input: music, mixed content, streaming.</summary>
    Audio = OPUS_APPLICATION_AUDIO,

    /// <summary>The lowest delay, with the speech-only modes left out.</summary>
    RestrictedLowDelay = OPUS_APPLICATION_RESTRICTED_LOWDELAY,

    /// <summary>The SILK modes only (libopus 1.6).</summary>
    RestrictedSilk = OPUS_APPLICATION_RESTRICTED_SILK,

    /// <summary>The CELT modes only (libopus 1.6).</summary>
    RestrictedCelt = OPUS_APPLICATION_RESTRICTED_CELT,
}

/// <summary>What the encoder is told the input is (<c>OPUS_SIGNAL_*</c>).</summary>
public enum OpusSignal
{
    /// <summary>The encoder decides from the input.</summary>
    Auto = OPUS_AUTO,

    /// <summary>Speech.</summary>
    Voice = OPUS_SIGNAL_VOICE,

    /// <summary>Music.</summary>
    Music = OPUS_SIGNAL_MUSIC,
}

/// <summary>An audio bandpass (<c>OPUS_BANDWIDTH_*</c>).</summary>
public enum OpusBandwidth
{
    /// <summary>The encoder decides.</summary>
    Auto = OPUS_AUTO,

    /// <summary>4 kHz.</summary>
    Narrowband = OPUS_BANDWIDTH_NARROWBAND,

    /// <summary>6 kHz.</summary>
    Mediumband = OPUS_BANDWIDTH_MEDIUMBAND,

    /// <summary>8 kHz.</summary>
    Wideband = OPUS_BANDWIDTH_WIDEBAND,

    /// <summary>12 kHz.</summary>
    SuperWideband = OPUS_BANDWIDTH_SUPERWIDEBAND,

    /// <summary>20 kHz.</summary>
    Fullband = OPUS_BANDWIDTH_FULLBAND,
}

/// <summary>Whether an encoder adds in-band forward error correction (<c>OPUS_SET_INBAND_FEC</c>).</summary>
public enum OpusInbandFec
{
    /// <summary>None (the default).</summary>
    Off = 0,

    /// <summary>
    /// On: past a high enough expected loss, the encoder switches to SILK even at high rates, so the
    /// redundancy can be carried.
    /// </summary>
    On = 1,

    /// <summary>On, without switching music to SILK for it.</summary>
    OnWithoutSilkSwitch = 2,
}

/// <summary>A frame duration the encoder may be held to (<c>OPUS_FRAMESIZE_*</c>).</summary>
public enum OpusFrameDuration
{
    /// <summary>The duration of the PCM handed to each encode (the default).</summary>
    FromInput = OPUS_FRAMESIZE_ARG,

    /// <summary>2.5 ms.</summary>
    Ms2_5 = OPUS_FRAMESIZE_2_5_MS,

    /// <summary>5 ms.</summary>
    Ms5 = OPUS_FRAMESIZE_5_MS,

    /// <summary>10 ms.</summary>
    Ms10 = OPUS_FRAMESIZE_10_MS,

    /// <summary>20 ms.</summary>
    Ms20 = OPUS_FRAMESIZE_20_MS,

    /// <summary>40 ms.</summary>
    Ms40 = OPUS_FRAMESIZE_40_MS,

    /// <summary>60 ms.</summary>
    Ms60 = OPUS_FRAMESIZE_60_MS,

    /// <summary>80 ms.</summary>
    Ms80 = OPUS_FRAMESIZE_80_MS,

    /// <summary>100 ms.</summary>
    Ms100 = OPUS_FRAMESIZE_100_MS,

    /// <summary>120 ms.</summary>
    Ms120 = OPUS_FRAMESIZE_120_MS,
}

/// <summary>A libopus error code (<c>OPUS_*</c> below zero).</summary>
public enum OpusError
{
    /// <summary>An argument was out of range.</summary>
    BadArgument = OPUS_BAD_ARG,

    /// <summary>The output buffer was too small.</summary>
    BufferTooSmall = OPUS_BUFFER_TOO_SMALL,

    /// <summary>libopus failed internally.</summary>
    InternalError = OPUS_INTERNAL_ERROR,

    /// <summary>The packet was corrupt or not Opus.</summary>
    InvalidPacket = OPUS_INVALID_PACKET,

    /// <summary>The request is not implemented.</summary>
    Unimplemented = OPUS_UNIMPLEMENTED,

    /// <summary>The state was invalid or freed.</summary>
    InvalidState = OPUS_INVALID_STATE,

    /// <summary>Memory could not be allocated.</summary>
    AllocationFailed = OPUS_ALLOC_FAIL,
}
