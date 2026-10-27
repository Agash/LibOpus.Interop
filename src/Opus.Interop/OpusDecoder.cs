using Opus.Interop.Interop;
using static Opus.Interop.Native.LibOpus;

namespace Opus.Interop;

/// <summary>
/// An Opus decoder (libopus <c>OpusDecoder</c>): packets in, PCM out, with loss concealment for packets
/// that never arrived and recovery from the in-band forward error correction of the next one.
/// </summary>
/// <remarks>
/// A decoder keeps state from packet to packet and is used from one thread at a time. Decoding and the
/// controls allocate nothing. An empty packet is a lost one: the decoder conceals it, as libopus does
/// for a null packet.
/// </remarks>
public sealed unsafe class OpusDecoder : IDisposable
{
    private readonly SafeDecoderHandle _handle;

    /// <summary>A decoder.</summary>
    /// <param name="sampleRate">The output's rate: 8000, 12000, 16000, 24000 or 48000 Hz.</param>
    /// <param name="channels">1 or 2.</param>
    /// <exception cref="OpusException">libopus refused the arguments.</exception>
    public OpusDecoder(int sampleRate, int channels)
    {
        CheckFormat(sampleRate, channels);
        _handle = SafeDecoderHandle.Create(sampleRate, channels);
        SampleRate = sampleRate;
        Channels = channels;
    }

    /// <summary>The output's sample rate.</summary>
    public int SampleRate { get; }

    /// <summary>The output's channels.</summary>
    public int Channels { get; }

    /// <summary>The gain applied to the output, in Q8 dB units (256 is 1 dB), -32768 to 32767.</summary>
    public int Gain
    {
        get => Get(OPUS_GET_GAIN_REQUEST);
        set => Set(OPUS_SET_GAIN_REQUEST, value);
    }

    /// <summary>Whether CELT's phase inversion for stereo is undone, which helps downmixing to mono.</summary>
    public bool PhaseInversionDisabled
    {
        get => Get(OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST) != 0;
        set => Set(OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST, value ? 1 : 0);
    }

    /// <summary>The bandpass of the last packet decoded.</summary>
    public OpusBandwidth Bandwidth => (OpusBandwidth)Get(OPUS_GET_BANDWIDTH_REQUEST);

    /// <summary>The samples per channel the last packet decoded to.</summary>
    public int LastPacketDuration => Get(OPUS_GET_LAST_PACKET_DURATION_REQUEST);

    /// <summary>The pitch period of the last frame, in samples at 48 kHz; 0 for unvoiced audio.</summary>
    public int Pitch => Get(OPUS_GET_PITCH_REQUEST);

    /// <summary>The range coder's final state for the last packet, which matches the encoder's.</summary>
    public uint FinalRange => (uint)Get(OPUS_GET_FINAL_RANGE_REQUEST);

    /// <summary>Decodes a packet into float samples, interleaved.</summary>
    /// <param name="packet">The packet; empty for one that was lost, which is concealed.</param>
    /// <param name="pcm">
    /// Where the samples go, with room for the packet's duration: <see cref="OpusPacket.MaxSamplesPerChannel"/>
    /// samples per channel hold any. For a lost packet, the duration to conceal.
    /// </param>
    /// <returns>The samples per channel written.</returns>
    /// <exception cref="OpusException">The packet is corrupt, or does not fit.</exception>
    public int Decode(ReadOnlySpan<byte> packet, Span<float> pcm) =>
        Decode(packet, pcm, fec: false);

    /// <summary>Decodes a packet into 16-bit samples, interleaved.</summary>
    /// <param name="packet">The packet; empty for one that was lost, which is concealed.</param>
    /// <param name="pcm">
    /// Where the samples go, with room for the packet's duration. For a lost packet, the duration to
    /// conceal.
    /// </param>
    /// <returns>The samples per channel written.</returns>
    /// <exception cref="OpusException">The packet is corrupt, or does not fit.</exception>
    public int Decode(ReadOnlySpan<byte> packet, Span<short> pcm) =>
        Decode(packet, pcm, fec: false);

    /// <summary>Decodes a packet into 24-bit samples held in 32-bit integers, interleaved.</summary>
    /// <param name="packet">The packet; empty for one that was lost, which is concealed.</param>
    /// <param name="pcm">
    /// Where the samples go, with room for the packet's duration. For a lost packet, the duration to
    /// conceal.
    /// </param>
    /// <returns>The samples per channel written.</returns>
    /// <exception cref="OpusException">The packet is corrupt, or does not fit.</exception>
    public int Decode24(ReadOnlySpan<byte> packet, Span<int> pcm) =>
        Decode(packet, pcm, fec: false);

    /// <summary>
    /// Recovers the packet before <paramref name="packet"/>, which was lost, from the in-band forward error
    /// correction <paramref name="packet"/> carries, into float samples. Without any, the loss is
    /// concealed. <paramref name="packet"/> itself is decoded by the next call.
    /// </summary>
    /// <param name="packet">The packet after the lost one.</param>
    /// <param name="pcm">Exactly the lost packet's duration.</param>
    /// <returns>The samples per channel written.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public int DecodeForwardErrorCorrection(ReadOnlySpan<byte> packet, Span<float> pcm) =>
        Decode(packet, pcm, fec: true);

    /// <summary>
    /// Recovers the packet before <paramref name="packet"/>, which was lost, from the forward error
    /// correction it carries, into 16-bit samples.
    /// </summary>
    /// <param name="packet">The packet after the lost one.</param>
    /// <param name="pcm">Exactly the lost packet's duration.</param>
    /// <returns>The samples per channel written.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public int DecodeForwardErrorCorrection(ReadOnlySpan<byte> packet, Span<short> pcm) =>
        Decode(packet, pcm, fec: true);

    /// <summary>
    /// Recovers the packet before <paramref name="packet"/>, which was lost, from the forward error
    /// correction it carries, into 24-bit samples held in 32-bit integers.
    /// </summary>
    /// <param name="packet">The packet after the lost one.</param>
    /// <param name="pcm">Exactly the lost packet's duration.</param>
    /// <returns>The samples per channel written.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public int DecodeForwardErrorCorrection24(ReadOnlySpan<byte> packet, Span<int> pcm) =>
        Decode(packet, pcm, fec: true);

    /// <summary>The samples per channel a packet decodes to at this decoder's rate.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The samples.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public int GetSampleCount(ReadOnlySpan<byte> packet)
    {
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                opus_decoder_get_nb_samples(Pointer, data, packet.Length)
            );
        }
    }

    /// <summary>Returns the decoder to its state when created, keeping its settings.</summary>
    public void Reset() =>
        _ = OpusException.ThrowIfError(opus_interop_decoder_ctl(Pointer, OPUS_RESET_STATE));

    /// <inheritdoc/>
    public void Dispose() => _handle.Dispose();

    private int Decode(ReadOnlySpan<byte> packet, Span<float> pcm, bool fec)
    {
        int frameSize = FrameSize(pcm.Length);
        fixed (byte* data = packet)
        fixed (float* samples = pcm)
        {
            return OpusException.ThrowIfError(
                opus_decode_float(Pointer, data, packet.Length, samples, frameSize, fec ? 1 : 0)
            );
        }
    }

    private int Decode(ReadOnlySpan<byte> packet, Span<short> pcm, bool fec)
    {
        int frameSize = FrameSize(pcm.Length);
        fixed (byte* data = packet)
        fixed (short* samples = pcm)
        {
            return OpusException.ThrowIfError(
                opus_decode(Pointer, data, packet.Length, samples, frameSize, fec ? 1 : 0)
            );
        }
    }

    private int Decode(ReadOnlySpan<byte> packet, Span<int> pcm, bool fec)
    {
        int frameSize = FrameSize(pcm.Length);
        fixed (byte* data = packet)
        fixed (int* samples = pcm)
        {
            return OpusException.ThrowIfError(
                opus_decode24(Pointer, data, packet.Length, samples, frameSize, fec ? 1 : 0)
            );
        }
    }

    // The rates and channel counts libopus codes; checked here so the message names the argument.
    internal static void CheckFormat(int sampleRate, int channels)
    {
        if (sampleRate is not (8000 or 12000 or 16000 or 24000 or 48000))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sampleRate),
                sampleRate,
                "Opus codes at 8000, 12000, 16000, 24000 or 48000 Hz."
            );
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
    }

    private Native.OpusDecoder* Pointer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
            return _handle.Pointer;
        }
    }

    private int FrameSize(int samples)
    {
        if (samples % Channels != 0)
        {
            throw new ArgumentException(
                $"{samples} samples do not divide into {Channels} channels.",
                "pcm"
            );
        }

        return samples / Channels;
    }

    private int Get(int request)
    {
        int value;
        _ = OpusException.ThrowIfError(opus_interop_decoder_ctl_get(Pointer, request, &value));
        return value;
    }

    private void Set(int request, int value) =>
        _ = OpusException.ThrowIfError(opus_interop_decoder_ctl_set(Pointer, request, value));
}
