namespace LibOpus.Interop.Tests;

[TestClass]
public sealed class CodecTests
{
    private const int Rate = 48_000;
    private const int Frame = 960; // 20 ms

    [TestMethod]
    public void Version_IsThePinnedLibopus() =>
        Assert.AreEqual("libopus 1.6.1", OpusLibrary.Version);

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void EncodeThenDecode_Float_ReproducesTheSignal(int channels)
    {
        using OpusEncoder encoder = new(Rate, channels, OpusApplication.Audio) { Bitrate = 96_000 };
        using OpusDecoder decoder = new(Rate, channels);
        float[] source = Tone(channels, frames: 50);
        float[] decoded = new float[source.Length];
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        for (int f = 0; f < 50; f++)
        {
            int length = encoder.Encode(
                source.AsSpan(f * Frame * channels, Frame * channels),
                packet
            );
            int samples = decoder.Decode(
                packet.AsSpan(0, length),
                decoded.AsSpan(f * Frame * channels, Frame * channels)
            );
            Assert.AreEqual(Frame, samples);
            Assert.AreEqual(encoder.FinalRange, decoder.FinalRange, "the range coders agree");
        }

        // The decoder's output trails the input by the encoder's lookahead.
        double correlation = Correlation(source, decoded, encoder.Lookahead * channels, channels);
        Assert.IsGreaterThan(0.99, correlation, $"correlation {correlation}");
    }

    [TestMethod]
    public void EncodeThenDecode_Int16And24_ReproduceTheSignal()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Audio) { Bitrate = 96_000 };
        using OpusDecoder decoder = new(Rate, 1);
        float[] tone = Tone(1, frames: 20);
        short[] pcm16 = [.. tone.Select(static s => (short)(s * 32767))];
        int[] pcm24 = [.. tone.Select(static s => (int)(s * 8_388_607))];
        short[] out16 = new short[pcm16.Length];
        int[] out24 = new int[pcm24.Length];
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        for (int f = 0; f < 10; f++)
        {
            int length = encoder.Encode(pcm16.AsSpan(f * Frame, Frame), packet);
            _ = decoder.Decode(packet.AsSpan(0, length), out16.AsSpan(f * Frame, Frame));
        }

        for (int f = 10; f < 20; f++)
        {
            int length = encoder.Encode24(pcm24.AsSpan(f * Frame, Frame), packet);
            _ = decoder.Decode24(packet.AsSpan(0, length), out24.AsSpan(f * Frame, Frame));
        }

        float[] decoded =
        [
            .. out16.Take(10 * Frame).Select(static s => s / 32767f),
            .. out24.Skip(10 * Frame).Select(static s => s / 8_388_607f),
        ];
        Assert.IsGreaterThan(0.99, Correlation(tone, decoded, encoder.Lookahead, 1));
    }

    [TestMethod]
    public void Controls_ReadBackWhatWasSet()
    {
        using OpusEncoder encoder = new(Rate, 2, OpusApplication.Voip)
        {
            Bitrate = 32_000,
            Complexity = 5,
            VariableBitrate = false,
            InbandFec = OpusInbandFec.OnWithoutSilkSwitch,
            PacketLossPercentage = 15,
            DiscontinuousTransmission = true,
            Signal = OpusSignal.Voice,
            MaxBandwidth = OpusBandwidth.Wideband,
            ForceChannels = 1,
            LsbDepth = 16,
            FrameDuration = OpusFrameDuration.Ms10,
            PredictionDisabled = true,
            PhaseInversionDisabled = true,
        };

        Assert.AreEqual(32_000, encoder.Bitrate);
        Assert.AreEqual(5, encoder.Complexity);
        Assert.IsFalse(encoder.VariableBitrate);
        Assert.AreEqual(OpusInbandFec.OnWithoutSilkSwitch, encoder.InbandFec);
        Assert.AreEqual(15, encoder.PacketLossPercentage);
        Assert.IsTrue(encoder.DiscontinuousTransmission);
        Assert.AreEqual(OpusSignal.Voice, encoder.Signal);
        Assert.AreEqual(OpusBandwidth.Wideband, encoder.MaxBandwidth);
        Assert.AreEqual(1, encoder.ForceChannels);
        Assert.AreEqual(16, encoder.LsbDepth);
        Assert.AreEqual(OpusFrameDuration.Ms10, encoder.FrameDuration);
        Assert.IsTrue(encoder.PredictionDisabled);
        Assert.IsTrue(encoder.PhaseInversionDisabled);
        Assert.AreEqual(OpusApplication.Voip, encoder.Application);

        encoder.ForceChannels = null;
        Assert.IsNull(encoder.ForceChannels);

        // The switches go back the other way as well.
        encoder.VariableBitrate = true;
        encoder.ConstrainedVariableBitrate = true;
        encoder.DiscontinuousTransmission = false;
        encoder.PredictionDisabled = false;
        encoder.PhaseInversionDisabled = false;
        Assert.IsTrue(encoder.VariableBitrate);
        Assert.IsTrue(encoder.ConstrainedVariableBitrate);
        Assert.IsFalse(encoder.DiscontinuousTransmission);
        Assert.IsFalse(encoder.PredictionDisabled);
        Assert.IsFalse(encoder.PhaseInversionDisabled);
        using OpusDecoder decoder = new(Rate, 2) { PhaseInversionDisabled = true };
        decoder.PhaseInversionDisabled = false;
        Assert.IsFalse(decoder.PhaseInversionDisabled);
        encoder.Reset();
        Assert.AreEqual(32_000, encoder.Bitrate, "a reset keeps the settings");
    }

    [TestMethod]
    public void Bitrate_ChangedBetweenFrames_ShrinksThePackets()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Audio)
        {
            Bitrate = 128_000,
            VariableBitrate = false,
        };
        float[] noise = Noise(frames: 40);
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        int high = 0;
        int low = 0;
        for (int f = 0; f < 40; f++)
        {
            if (f == 20)
            {
                encoder.Bitrate = 16_000;
            }

            int length = encoder.Encode(noise.AsSpan(f * Frame, Frame), packet);
            if (f is >= 5 and < 20)
            {
                high += length;
            }
            else if (f >= 25)
            {
                low += length;
            }
        }

        // Constant bitrate: 128 and 16 kbit/s are 320 and 40 bytes a 20 ms frame.
        Assert.AreEqual(15 * 320, high, 15 * 4);
        Assert.AreEqual(15 * 40, low, 15 * 4);
    }

    [TestMethod]
    public void InbandFec_RecoversALostPacket()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Voip)
        {
            Bitrate = 24_000,
            InbandFec = OpusInbandFec.On,
            PacketLossPercentage = 20,
            Signal = OpusSignal.Voice,
        };
        using OpusDecoder decoder = new(Rate, 1);
        float[] voice = Tone(1, frames: 30, frequency: 220);
        byte[][] packets = new byte[30][];
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        for (int f = 0; f < 30; f++)
        {
            int length = encoder.Encode(voice.AsSpan(f * Frame, Frame), packet);
            packets[f] = packet[..length];
        }

        Assert.IsTrue(
            packets.Skip(5).Any(static p => OpusPacket.HasForwardErrorCorrection(p)),
            "packets carry redundancy for the one before"
        );
        float[] pcm = new float[Frame];
        for (int f = 0; f < 20; f++)
        {
            _ = decoder.Decode(packets[f], pcm);
        }

        // Packet 20 is lost; packet 21 brings it back.
        Assert.AreEqual(Frame, decoder.DecodeForwardErrorCorrection(packets[21], pcm));
        Assert.IsGreaterThan(0.01, Rms(pcm), "the recovered frame carries the signal");
        Assert.AreEqual(Frame, decoder.Decode(packets[21], pcm));
    }

    [TestMethod]
    public void Decode_EmptyPacket_ConcealsTheLoss()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Audio);
        using OpusDecoder decoder = new(Rate, 1);
        float[] tone = Tone(1, frames: 10);
        float[] pcm = new float[Frame];
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        for (int f = 0; f < 10; f++)
        {
            int length = encoder.Encode(tone.AsSpan(f * Frame, Frame), packet);
            _ = decoder.Decode(packet.AsSpan(0, length), pcm);
        }

        Assert.AreEqual(Frame, decoder.Decode([], pcm));
        Assert.IsGreaterThan(0.01, Rms(pcm), "the concealment continues the tone");
    }

    [TestMethod]
    public void Packet_IsReadFromItsTableOfContents()
    {
        using OpusEncoder encoder = new(Rate, 2, OpusApplication.Audio) { Bitrate = 128_000 };
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        int length = encoder.Encode(Tone(2, frames: 1), packet);
        ReadOnlySpan<byte> encoded = packet.AsSpan(0, length);

        Assert.AreEqual(2, OpusPacket.GetChannelCount(encoded));
        Assert.AreEqual(1, OpusPacket.GetFrameCount(encoded));
        Assert.AreEqual(Frame, OpusPacket.GetSampleCount(encoded, Rate));
        Assert.AreEqual(Frame, OpusPacket.GetSamplesPerFrame(encoded, Rate));
        Assert.AreEqual(OpusBandwidth.Fullband, OpusPacket.GetBandwidth(encoded));
        using OpusDecoder decoder = new(Rate, 2);
        Assert.AreEqual(Frame, decoder.GetSampleCount(encoded));
    }

    [TestMethod]
    public void EncodeAndDecode_AllocateNothing()
    {
        using OpusEncoder encoder = new(Rate, 2, OpusApplication.Audio);
        using OpusDecoder decoder = new(Rate, 2);
        float[] tone = Tone(2, frames: 1);
        float[] pcm = new float[Frame * 2];
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        _ = decoder.Decode(packet.AsSpan(0, encoder.Encode(tone, packet)), pcm);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            int length = encoder.Encode(tone, packet);
            _ = decoder.Decode(packet.AsSpan(0, length), pcm);
            encoder.Bitrate = 64_000 + i;
        }

        Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [TestMethod]
    public void DecoderControls_ReadBackAndReportTheLastPacket()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Voip)
        {
            ConstrainedVariableBitrate = false,
            DiscontinuousTransmission = true,
        };
        using OpusDecoder decoder = new(Rate, 1) { Gain = 256, PhaseInversionDisabled = true };
        Assert.AreEqual(256, decoder.Gain);
        Assert.IsTrue(decoder.PhaseInversionDisabled);
        Assert.IsFalse(encoder.ConstrainedVariableBitrate);

        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        float[] pcm = new float[Frame];
        float[] voice = Tone(1, frames: 10, frequency: 220);
        for (int f = 0; f < 10; f++)
        {
            int length = encoder.Encode(voice.AsSpan(f * Frame, Frame), packet);
            _ = decoder.Decode(packet.AsSpan(0, length), pcm);
        }

        Assert.AreEqual(Frame, decoder.LastPacketDuration);
        Assert.AreNotEqual(OpusBandwidth.Auto, decoder.Bandwidth);
        Assert.IsGreaterThanOrEqualTo(0, decoder.Pitch);
        encoder.Bandwidth = OpusBandwidth.Wideband;
        _ = encoder.Encode(voice.AsSpan(0, Frame), packet);
        Assert.AreEqual(
            OpusBandwidth.Wideband,
            encoder.Bandwidth,
            "the last frame was coded at it"
        );
        Assert.IsFalse(encoder.InDiscontinuousTransmission, "a tone is not silence");

        // Silence under DTX is coded as such once the encoder has seen enough of it.
        for (int f = 0; f < 30; f++)
        {
            _ = encoder.Encode(new float[Frame], packet);
        }

        Assert.IsTrue(encoder.InDiscontinuousTransmission);
        decoder.Reset();
        Assert.AreEqual(256, decoder.Gain, "a reset keeps the settings");
    }

    [TestMethod]
    public void ForwardErrorCorrection_Int16And24_RecoverALostPacket()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Voip)
        {
            Bitrate = 24_000,
            InbandFec = OpusInbandFec.On,
            PacketLossPercentage = 20,
        };
        using OpusDecoder decoder = new(Rate, 1);
        float[] voice = Tone(1, frames: 12, frequency: 220);
        byte[][] packets = new byte[12][];
        byte[] packet = new byte[OpusPacket.MaxFrameBytes];
        for (int f = 0; f < 12; f++)
        {
            packets[f] = packet[..encoder.Encode(voice.AsSpan(f * Frame, Frame), packet)];
        }

        short[] pcm16 = new short[Frame];
        int[] pcm24 = new int[Frame];
        for (int f = 0; f < 8; f++)
        {
            _ = decoder.Decode(packets[f], pcm16);
        }

        Assert.AreEqual(Frame, decoder.DecodeForwardErrorCorrection(packets[9], pcm16));
        Assert.AreEqual(Frame, decoder.Decode(packets[9], pcm16));
        Assert.AreEqual(Frame, decoder.DecodeForwardErrorCorrection24(packets[11], pcm24));
        Assert.AreEqual(Frame, decoder.Decode24(packets[11], pcm24));
    }

    [TestMethod]
    public void Packet_EmptyOrCorrupt_IsRefused()
    {
        Assert.AreEqual(
            OpusError.InvalidPacket,
            Assert.ThrowsExactly<OpusException>(() => OpusPacket.GetBandwidth([])).Error
        );
        Assert.AreEqual(
            OpusError.InvalidPacket,
            Assert.ThrowsExactly<OpusException>(() => OpusPacket.GetChannelCount([])).Error
        );
        Assert.AreEqual(
            OpusError.InvalidPacket,
            Assert.ThrowsExactly<OpusException>(() => OpusPacket.GetSamplesPerFrame([], Rate)).Error
        );
        _ = Assert.ThrowsExactly<OpusException>(() => OpusPacket.GetFrameCount([]));
        _ = Assert.ThrowsExactly<OpusException>(() => OpusPacket.GetSampleCount([], Rate));
        Assert.IsFalse(OpusPacket.HasForwardErrorCorrection([0x80]), "a CELT packet carries none");
    }

    [TestMethod]
    public void Decoder_SamplesNotWholeFrames_AreRefused()
    {
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OpusDecoder(Rate, 0));
        using OpusDecoder decoder = new(Rate, 2);
        _ = Assert.ThrowsExactly<ArgumentException>(() =>
            decoder.Decode([0x80], new float[(Frame * 2) + 1])
        );
        _ = Assert.ThrowsExactly<ArgumentException>(() => decoder.Decode([0x80], new short[3]));
    }

    [TestMethod]
    public void Exception_CarriesTheErrorAndMessage()
    {
        OpusException error = new(OpusError.InvalidPacket);
        Assert.AreEqual(OpusError.InvalidPacket, error.Error);
        StringAssert.Contains(error.Message, "corrupted");
        Assert.AreEqual(OpusError.InternalError, new OpusException().Error);
        Assert.AreEqual("why", new OpusException("why").Message);
        InvalidOperationException cause = new();
        Assert.AreSame(cause, new OpusException("why", cause).InnerException);
    }

    [TestMethod]
    public void Errors_AreTyped()
    {
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new OpusEncoder(44_100, 2, OpusApplication.Audio)
        );
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OpusDecoder(Rate, 3));
        using OpusEncoder encoder = new(Rate, 2, OpusApplication.Audio);
        _ = Assert.ThrowsExactly<ArgumentException>(() =>
            encoder.Encode(new float[(Frame * 2) + 1], new byte[OpusPacket.MaxFrameBytes])
        );
        OpusException duration = Assert.ThrowsExactly<OpusException>(() =>
            encoder.Encode(new float[100 * 2], new byte[OpusPacket.MaxFrameBytes])
        );
        Assert.AreEqual(OpusError.BadArgument, duration.Error);
        OpusException complexity = Assert.ThrowsExactly<OpusException>(() =>
            encoder.Complexity = 11
        );
        Assert.AreEqual(OpusError.BadArgument, complexity.Error);

        using OpusDecoder decoder = new(Rate, 2);
        OpusException corrupt = Assert.ThrowsExactly<OpusException>(() =>
            decoder.Decode([0xFF, 0xFF, 0xFF], new float[Frame * 2])
        );
        Assert.AreEqual(OpusError.InvalidPacket, corrupt.Error);

        decoder.Dispose();
        _ = Assert.ThrowsExactly<ObjectDisposedException>(() => decoder.Gain);
    }

    private static float[] Tone(int channels, int frames, double frequency = 440)
    {
        float[] samples = new float[frames * Frame * channels];
        for (int i = 0; i < frames * Frame; i++)
        {
            float value = (float)(0.4 * Math.Sin(2 * Math.PI * frequency * i / Rate));
            for (int c = 0; c < channels; c++)
            {
                samples[(i * channels) + c] = value;
            }
        }

        return samples;
    }

    private static float[] Noise(int frames)
    {
        Random random = new(7);
        return
        [
            .. Enumerable
                .Range(0, frames * Frame)
                .Select(_ => (float)((random.NextDouble() * 2) - 1) * 0.5f),
        ];
    }

    // The correlation of the source with the decoded signal shifted back by the delay, past the first
    // frame, where the encoder is still settling.
    private static double Correlation(float[] source, float[] decoded, int delay, int channels)
    {
        double sum = 0;
        double a = 0;
        double b = 0;
        for (int i = Frame * channels; i + delay < decoded.Length; i++)
        {
            sum += source[i] * decoded[i + delay];
            a += source[i] * source[i];
            b += decoded[i + delay] * decoded[i + delay];
        }

        return sum / Math.Sqrt(a * b);
    }

    private static double Rms(float[] samples) =>
        Math.Sqrt(samples.Sum(static s => (double)s * s) / samples.Length);
}
