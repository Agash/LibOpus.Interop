using System.Runtime.InteropServices;
using static LibOpus.Interop.Native.Opus;

namespace LibOpus.Interop;

/// <summary>What an Opus packet holds, read from its table of contents without decoding it.</summary>
public static unsafe class OpusPacket
{
    /// <summary>The largest packet a single frame codes to.</summary>
    public const int MaxFrameBytes = 1275;

    /// <summary>The most samples per channel one packet decodes to: 120 ms at 48 kHz.</summary>
    public const int MaxSamplesPerChannel = 5760;

    /// <summary>The packet's bandpass.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The bandpass.</returns>
    /// <exception cref="OpusException">The packet is empty or corrupt.</exception>
    public static OpusBandwidth GetBandwidth(ReadOnlySpan<byte> packet)
    {
        fixed (byte* data = packet)
        {
            return (OpusBandwidth)
                OpusException.ThrowIfError(
                    packet.IsEmpty ? OPUS_INVALID_PACKET : opus_packet_get_bandwidth(data)
                );
        }
    }

    /// <summary>The channels the packet codes.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>1 or 2.</returns>
    /// <exception cref="OpusException">The packet is empty or corrupt.</exception>
    public static int GetChannelCount(ReadOnlySpan<byte> packet)
    {
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                packet.IsEmpty ? OPUS_INVALID_PACKET : opus_packet_get_nb_channels(data)
            );
        }
    }

    /// <summary>The frames in the packet.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The frames.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public static int GetFrameCount(ReadOnlySpan<byte> packet)
    {
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(opus_packet_get_nb_frames(data, packet.Length));
        }
    }

    /// <summary>The samples per channel the packet decodes to at a sample rate.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="sampleRate">The decoder's rate.</param>
    /// <returns>The samples.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public static int GetSampleCount(ReadOnlySpan<byte> packet, int sampleRate)
    {
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                opus_packet_get_nb_samples(data, packet.Length, sampleRate)
            );
        }
    }

    /// <summary>The samples per channel in each of the packet's frames at a sample rate.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="sampleRate">The decoder's rate.</param>
    /// <returns>The samples.</returns>
    /// <exception cref="OpusException">The packet is empty.</exception>
    public static int GetSamplesPerFrame(ReadOnlySpan<byte> packet, int sampleRate)
    {
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(
                packet.IsEmpty
                    ? OPUS_INVALID_PACKET
                    : opus_packet_get_samples_per_frame(data, sampleRate)
            );
        }
    }

    /// <summary>
    /// Whether the packet carries in-band forward error correction (SILK's low-bitrate redundancy) for
    /// the packet before it, which a decoder recovers with <c>decodeFec</c>.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>Whether it does.</returns>
    /// <exception cref="OpusException">The packet is corrupt.</exception>
    public static bool HasForwardErrorCorrection(ReadOnlySpan<byte> packet)
    {
        fixed (byte* data = packet)
        {
            return OpusException.ThrowIfError(opus_packet_has_lbrr(data, packet.Length)) != 0;
        }
    }
}

/// <summary>The libopus build the bindings run on.</summary>
public static unsafe class OpusLibrary
{
    /// <summary>libopus's version string, such as <c>libopus 1.6.1</c>.</summary>
    public static string Version => Marshal.PtrToStringUTF8((nint)opus_get_version_string())!;
}
