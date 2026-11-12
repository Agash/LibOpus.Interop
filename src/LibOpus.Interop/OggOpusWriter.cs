using System.Buffers.Binary;
using System.Text;

namespace LibOpus.Interop;

/// <summary>
/// Writes Opus packets as an Ogg Opus stream (RFC 7845 on the Ogg container of RFC 3533): the identification header
/// and the comment header on their own pages, then the audio, closed by an end-of-stream page whose granule
/// position trims the encoder's padding. The result plays in browsers as <c>audio/ogg; codecs=opus</c>.
/// </summary>
/// <remarks>Channel mapping family 0 (mono or stereo). One writer writes one logical stream; it is not thread safe.</remarks>
public sealed class OggOpusWriter
{
    // RFC 3533 section 6: a page holds at most 255 lacing values; packets are gathered until a page reaches this
    // size, which keeps the per-page overhead small without delaying a live stream by much.
    private const int MaxSegments = 255;
    private const int PageTarget = 4096;
    private const byte BeginningOfStream = 0x02;
    private const byte EndOfStream = 0x04;

    private static readonly uint[] _crcTable = BuildCrcTable();

    private readonly Stream _output;
    private readonly uint _serial;
    private readonly List<byte> _segments = new(MaxSegments);
    private readonly MemoryStream _body = new();
    private uint _sequence;
    private long _granule;
    private bool _finished;

    /// <summary>A writer that writes both headers to <paramref name="output"/> now.</summary>
    /// <param name="output">Where the stream goes.</param>
    /// <param name="channels">1 or 2.</param>
    /// <param name="inputSampleRate">The rate the audio had before encoding, recorded for players that resample back.</param>
    /// <param name="preSkip">
    /// Samples at 48 kHz the decoder drops from the start: the encoder's <see cref="OpusEncoder.Lookahead"/> converted to
    /// 48 kHz (RFC 7845 section 4.2).
    /// </param>
    /// <param name="serial">The logical stream's serial number; a random one when null.</param>
    /// <param name="vendor">The vendor string of the comment header.</param>
    public OggOpusWriter(Stream output, int channels, int inputSampleRate, int preSkip, uint? serial = null, string vendor = "LibOpus.Interop")
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(preSkip);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(preSkip, ushort.MaxValue);
        ArgumentNullException.ThrowIfNull(vendor);
        _output = output;
        _serial = serial ?? (uint)Random.Shared.NextInt64(0, uint.MaxValue);
        PreSkip = preSkip;
        _granule = preSkip;

        // RFC 7845 section 5.1: the identification header, alone on the first page.
        byte[] head = new byte[19];
        "OpusHead"u8.CopyTo(head);
        head[8] = 1;
        head[9] = (byte)channels;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(10), (ushort)preSkip);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(12), (uint)inputSampleRate);
        AddPacket(head);
        FlushPage(BeginningOfStream, granule: 0);

        // Section 5.2: the comment header, ending on its own page; no user comments.
        byte[] vendorBytes = Encoding.UTF8.GetBytes(vendor);
        byte[] tags = new byte[8 + 4 + vendorBytes.Length + 4];
        "OpusTags"u8.CopyTo(tags);
        BinaryPrimitives.WriteUInt32LittleEndian(tags.AsSpan(8), (uint)vendorBytes.Length);
        vendorBytes.CopyTo(tags, 12);
        AddPacket(tags);
        FlushPage(0, granule: 0);
    }

    /// <summary>The pre-skip in the identification header.</summary>
    public int PreSkip { get; }

    /// <summary>Adds an Opus packet; full pages are written as they fill.</summary>
    /// <param name="packet">One Opus packet as <see cref="OpusEncoder"/> produced it.</param>
    public void WritePacket(ReadOnlySpan<byte> packet)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        if (packet.IsEmpty)
        {
            throw new ArgumentException("An Opus packet has at least one byte.", nameof(packet));
        }

        int lacing = (packet.Length / 255) + 1;
        if (_segments.Count + lacing > MaxSegments)
        {
            FlushPage(0, _granule);
        }

        _granule += OpusPacket.GetSampleCount(packet, 48_000);
        AddPacket(packet);
        if (_body.Length >= PageTarget)
        {
            FlushPage(0, _granule);
        }
    }

    /// <summary>
    /// Writes the last page, marked end of stream. Its granule position ends playback after
    /// <paramref name="sampleCount48k"/> samples (the original length at 48 kHz), which trims the encoder's padding in
    /// the final packet (RFC 7845 section 4.4); null keeps every decoded sample.
    /// </summary>
    public void Finish(long? sampleCount48k = null)
    {
        if (_finished)
        {
            return;
        }

        long end = sampleCount48k is { } count ? Math.Min(_granule, PreSkip + count) : _granule;
        FlushPage(EndOfStream, end, force: true);
        _finished = true;
        _output.Flush();
    }

    private void AddPacket(ReadOnlySpan<byte> packet)
    {
        // Lacing: 255 for each full segment, then the remainder; a packet that is a multiple of 255 ends with 0.
        int remaining = packet.Length;
        while (remaining >= 255)
        {
            _segments.Add(255);
            remaining -= 255;
        }

        _segments.Add((byte)remaining);
        _body.Write(packet);
    }

    private void FlushPage(byte flags, long granule, bool force = false)
    {
        if (_segments.Count == 0 && !force)
        {
            return;
        }

        int headerLength = 27 + _segments.Count;
        byte[] page = new byte[headerLength + (int)_body.Length];
        "OggS"u8.CopyTo(page);
        page[4] = 0;
        page[5] = flags;
        BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(6), granule);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(14), _serial);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(18), _sequence++);
        page[26] = (byte)_segments.Count;
        for (int i = 0; i < _segments.Count; i++)
        {
            page[27 + i] = _segments[i];
        }

        _body.Position = 0;
        _body.ReadExactly(page.AsSpan(headerLength));
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(22), Crc(page));
        _output.Write(page);
        _segments.Clear();
        _body.SetLength(0);
    }

    // RFC 3533 section 6: CRC-32 with polynomial 0x04C11DB7, initial value 0, no reflection and no final XOR, over the
    // whole page with the checksum field zero.
    private static uint Crc(ReadOnlySpan<byte> page)
    {
        uint crc = 0;
        foreach (byte value in page)
        {
            crc = (crc << 8) ^ _crcTable[(byte)(crc >> 24) ^ value];
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i << 24;
            for (int bit = 0; bit < 8; bit++)
            {
                r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
            }

            table[i] = r;
        }

        return table;
    }
}
