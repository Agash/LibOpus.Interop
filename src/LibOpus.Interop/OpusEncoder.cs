using LibOpus.Interop.Interop;
using static LibOpus.Interop.Native.Opus;

namespace LibOpus.Interop;

/// <summary>
/// An Opus encoder (libopus <c>OpusEncoder</c>): PCM frames in, Opus packets out. Each call encodes one
/// frame of 2.5, 5, 10, 20, 40 or 60 ms (or up to 120 ms in multiples of 20) into one packet.
/// </summary>
/// <remarks>
/// An encoder keeps state from frame to frame and is used from one thread at a time. Encoding and the
/// controls allocate nothing.
/// </remarks>
public sealed unsafe class OpusEncoder : IDisposable
{
    /// <summary>The bitrate the encoder chooses for itself (<c>OPUS_AUTO</c>).</summary>
    public const int AutoBitrate = OPUS_AUTO;

    /// <summary>The most the encoder can spend (<c>OPUS_BITRATE_MAX</c>).</summary>
    public const int MaxBitrate = OPUS_BITRATE_MAX;

    private readonly SafeEncoderHandle _handle;

    /// <summary>An encoder.</summary>
    /// <param name="sampleRate">The input's rate: 8000, 12000, 16000, 24000 or 48000 Hz.</param>
    /// <param name="channels">1 or 2.</param>
    /// <param name="application">What the encoder is tuned for.</param>
    /// <exception cref="OpusException">libopus refused the arguments.</exception>
    public OpusEncoder(int sampleRate, int channels, OpusApplication application)
    {
        OpusDecoder.CheckFormat(sampleRate, channels);
        _handle = SafeEncoderHandle.Create(sampleRate, channels, application);
        SampleRate = sampleRate;
        Channels = channels;
    }

    /// <summary>The input's sample rate.</summary>
    public int SampleRate { get; }

    /// <summary>The input's channels.</summary>
    public int Channels { get; }

    /// <summary>What the encoder is tuned for; it can change between frames.</summary>
    public OpusApplication Application
    {
        get => (OpusApplication)Get(OPUS_GET_APPLICATION_REQUEST);
        set => Set(OPUS_SET_APPLICATION_REQUEST, (int)value);
    }

    /// <summary>
    /// The bitrate in bits per second (500 to 512000), or <see cref="AutoBitrate"/> or
    /// <see cref="MaxBitrate"/>. It takes effect from the next frame.
    /// </summary>
    public int Bitrate
    {
        get => Get(OPUS_GET_BITRATE_REQUEST);
        set => Set(OPUS_SET_BITRATE_REQUEST, value);
    }

    /// <summary>The computational complexity, 0 to 10; higher is better and slower.</summary>
    public int Complexity
    {
        get => Get(OPUS_GET_COMPLEXITY_REQUEST);
        set => Set(OPUS_SET_COMPLEXITY_REQUEST, value);
    }

    /// <summary>Whether the bitrate varies with the input (the default) or holds constant.</summary>
    public bool VariableBitrate
    {
        get => Get(OPUS_GET_VBR_REQUEST) != 0;
        set => Set(OPUS_SET_VBR_REQUEST, value ? 1 : 0);
    }

    /// <summary>Whether a variable bitrate is held to the target over short spans (the default).</summary>
    public bool ConstrainedVariableBitrate
    {
        get => Get(OPUS_GET_VBR_CONSTRAINT_REQUEST) != 0;
        set => Set(OPUS_SET_VBR_CONSTRAINT_REQUEST, value ? 1 : 0);
    }

    /// <summary>Whether frames carry in-band forward error correction for the one before.</summary>
    public OpusInbandFec InbandFec
    {
        get => (OpusInbandFec)Get(OPUS_GET_INBAND_FEC_REQUEST);
        set => Set(OPUS_SET_INBAND_FEC_REQUEST, (int)value);
    }

    /// <summary>The packet loss expected, 0 to 100 percent, which tunes the error correction.</summary>
    public int PacketLossPercentage
    {
        get => Get(OPUS_GET_PACKET_LOSS_PERC_REQUEST);
        set => Set(OPUS_SET_PACKET_LOSS_PERC_REQUEST, value);
    }

    /// <summary>Whether silence is sent as rare tiny packets (discontinuous transmission).</summary>
    public bool DiscontinuousTransmission
    {
        get => Get(OPUS_GET_DTX_REQUEST) != 0;
        set => Set(OPUS_SET_DTX_REQUEST, value ? 1 : 0);
    }

    /// <summary>Whether the last frame was coded as silence under discontinuous transmission.</summary>
    public bool InDiscontinuousTransmission => Get(OPUS_GET_IN_DTX_REQUEST) != 0;

    /// <summary>What the input is, as a hint to the mode decision.</summary>
    public OpusSignal Signal
    {
        get => (OpusSignal)Get(OPUS_GET_SIGNAL_REQUEST);
        set => Set(OPUS_SET_SIGNAL_REQUEST, (int)value);
    }

    /// <summary>The widest bandpass the encoder may choose.</summary>
    public OpusBandwidth MaxBandwidth
    {
        get => (OpusBandwidth)Get(OPUS_GET_MAX_BANDWIDTH_REQUEST);
        set => Set(OPUS_SET_MAX_BANDWIDTH_REQUEST, (int)value);
    }

    /// <summary>
    /// The bandpass coming frames are coded at, or <see cref="OpusBandwidth.Auto"/> to let the encoder
    /// choose it. Read back, it is the bandpass of the last frame coded (<c>OPUS_GET_BANDWIDTH</c>).
    /// </summary>
    public OpusBandwidth Bandwidth
    {
        get => (OpusBandwidth)Get(OPUS_GET_BANDWIDTH_REQUEST);
        set => Set(OPUS_SET_BANDWIDTH_REQUEST, (int)value);
    }

    /// <summary>The channels coded: 1 or 2 to force them, null to follow the input.</summary>
    public int? ForceChannels
    {
        get => Get(OPUS_GET_FORCE_CHANNELS_REQUEST) is var forced and not OPUS_AUTO ? forced : null;
        set => Set(OPUS_SET_FORCE_CHANNELS_REQUEST, value ?? OPUS_AUTO);
    }

    /// <summary>The input's significant bits, 8 to 24, which spares bits on quieter detail.</summary>
    public int LsbDepth
    {
        get => Get(OPUS_GET_LSB_DEPTH_REQUEST);
        set => Set(OPUS_SET_LSB_DEPTH_REQUEST, value);
    }

    /// <summary>A frame duration the encoder is held to, below the input's.</summary>
    public OpusFrameDuration FrameDuration
    {
        get => (OpusFrameDuration)Get(OPUS_GET_EXPERT_FRAME_DURATION_REQUEST);
        set => Set(OPUS_SET_EXPERT_FRAME_DURATION_REQUEST, (int)value);
    }

    /// <summary>
    /// Whether frames are coded without reference to earlier ones, so each decodes alone at a cost in
    /// quality.
    /// </summary>
    public bool PredictionDisabled
    {
        get => Get(OPUS_GET_PREDICTION_DISABLED_REQUEST) != 0;
        set => Set(OPUS_SET_PREDICTION_DISABLED_REQUEST, value ? 1 : 0);
    }

    /// <summary>Whether CELT's phase inversion for stereo is off, which helps downmixing to mono.</summary>
    public bool PhaseInversionDisabled
    {
        get => Get(OPUS_GET_PHASE_INVERSION_DISABLED_REQUEST) != 0;
        set => Set(OPUS_SET_PHASE_INVERSION_DISABLED_REQUEST, value ? 1 : 0);
    }

    /// <summary>The samples per channel the encoder delays its output by.</summary>
    public int Lookahead => Get(OPUS_GET_LOOKAHEAD_REQUEST);

    /// <summary>The range coder's final state for the last frame, which a decoder matches.</summary>
    public uint FinalRange => (uint)Get(OPUS_GET_FINAL_RANGE_REQUEST);

    /// <summary>Encodes one frame of float samples, interleaved, nominally in [-1, 1].</summary>
    /// <param name="pcm">The frame: its length divided by <see cref="Channels"/> is its duration in samples.</param>
    /// <param name="packet">Where the packet goes; 1275 bytes holds any single frame.</param>
    /// <returns>The packet's length in bytes.</returns>
    /// <exception cref="OpusException">The frame's duration is not one Opus codes, or the packet does not fit.</exception>
    public int Encode(ReadOnlySpan<float> pcm, Span<byte> packet)
    {
        int frameSize = FrameSize(pcm.Length);
        fixed (float* samples = pcm)
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                opus_encode_float(Pointer, samples, frameSize, data, packet.Length)
            );
        }
    }

    /// <summary>Encodes one frame of 16-bit samples, interleaved.</summary>
    /// <param name="pcm">The frame: its length divided by <see cref="Channels"/> is its duration in samples.</param>
    /// <param name="packet">Where the packet goes; 1275 bytes holds any single frame.</param>
    /// <returns>The packet's length in bytes.</returns>
    /// <exception cref="OpusException">The frame's duration is not one Opus codes, or the packet does not fit.</exception>
    public int Encode(ReadOnlySpan<short> pcm, Span<byte> packet)
    {
        int frameSize = FrameSize(pcm.Length);
        fixed (short* samples = pcm)
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                opus_encode(Pointer, samples, frameSize, data, packet.Length)
            );
        }
    }

    /// <summary>Encodes one frame of 24-bit samples held in 32-bit integers, interleaved.</summary>
    /// <param name="pcm">The frame: its length divided by <see cref="Channels"/> is its duration in samples.</param>
    /// <param name="packet">Where the packet goes; 1275 bytes holds any single frame.</param>
    /// <returns>The packet's length in bytes.</returns>
    /// <exception cref="OpusException">The frame's duration is not one Opus codes, or the packet does not fit.</exception>
    public int Encode24(ReadOnlySpan<int> pcm, Span<byte> packet)
    {
        int frameSize = FrameSize(pcm.Length);
        fixed (int* samples = pcm)
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                opus_encode24(Pointer, samples, frameSize, data, packet.Length)
            );
        }
    }

    /// <summary>Returns the encoder to its state when created, keeping its settings.</summary>
    public void Reset() =>
        _ = OpusException.ThrowIfError(opus_interop_encoder_ctl(Pointer, OPUS_RESET_STATE));

    /// <inheritdoc/>
    public void Dispose() => _handle.Dispose();

    private Native.OpusEncoder* Pointer
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
        _ = OpusException.ThrowIfError(opus_interop_encoder_ctl_get(Pointer, request, &value));
        return value;
    }

    private void Set(int request, int value) =>
        _ = OpusException.ThrowIfError(opus_interop_encoder_ctl_set(Pointer, request, value));
}
