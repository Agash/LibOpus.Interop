using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace LibOpus.Interop;

/// <summary>
/// Writes an Ogg Opus stream (RFC 7845 on the Ogg container of RFC 3533) into an <see cref="IBufferWriter{T}"/>: the
/// identification and comment headers on their own pages, then the audio, closed by an end-of-stream page whose
/// granule position trims the encoder's padding. The result plays in browsers as <c>audio/ogg; codecs=opus</c>.
/// </summary>
/// <remarks>
/// <para>
/// Packets are encoded straight into the page being built (<see cref="GetPacketSpan"/>, then <see cref="Advance"/>), and
/// each finished page is copied once into the output. After construction nothing is allocated: the page buffer comes
/// from <see cref="ArrayPool{T}"/> and goes back on <see cref="Dispose"/>.
/// </para>
/// <para>Channel mapping family 0 (mono or stereo). One writer writes one logical stream from one thread.</para>
/// </remarks>
public sealed class OggOpusWriter : IDisposable
{
    // RFC 3533 section 6: at most 255 lacing values per page. Packets gather until a page's body reaches PageTarget,
    // which keeps the 27-byte page overhead small without holding a live stream back by much.
    private const int MaxSegments = 255;
    private const int PageTarget = 4096;
    private const int MaxHeader = 27 + MaxSegments;
    private const int BodyCapacity = PageTarget + OpusPacket.MaxFrameBytes;

    // An Opus packet of at most 1275 bytes takes at most six lacing values.
    private const int MaxPacketSegments = (OpusPacket.MaxFrameBytes / 255) + 1;
    private const int MaxVendorBytes = 1024;
    private const byte BeginningOfStream = 0x02;
    private const byte EndOfStream = 0x04;

    private readonly IBufferWriter<byte> _output;
    private readonly uint _serial;
    private byte[]? _page;
    private int _segmentCount;
    private int _bodyLength;
    private uint _sequence;
    private long _granule;
    private bool _finished;

    /// <summary>A writer that writes both headers to <paramref name="output"/> now.</summary>
    /// <param name="output">Where the pages go.</param>
    /// <param name="channels">1 or 2.</param>
    /// <param name="inputSampleRate">The rate the audio had before encoding, recorded for players that resample back.</param>
    /// <param name="preSkip">
    /// Samples at 48 kHz the decoder drops from the start: the encoder's <see cref="OpusEncoder.Lookahead"/> at 48 kHz
    /// (RFC 7845 section 4.2).
    /// </param>
    /// <param name="serial">The logical stream's serial number; a random one when null.</param>
    /// <param name="vendor">The vendor string of the comment header, at most 1024 bytes as UTF-8.</param>
    public OggOpusWriter(IBufferWriter<byte> output, int channels, int inputSampleRate, int preSkip, uint? serial = null, string vendor = "LibOpus.Interop")
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegative(preSkip);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(preSkip, ushort.MaxValue);
        ArgumentNullException.ThrowIfNull(vendor);
        int vendorLength = Encoding.UTF8.GetByteCount(vendor);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vendorLength, MaxVendorBytes, nameof(vendor));

        _output = output;
        _serial = serial ?? (uint)Random.Shared.NextInt64(0, uint.MaxValue);
        _page = ArrayPool<byte>.Shared.Rent(MaxHeader + BodyCapacity);
        PreSkip = preSkip;
        _granule = preSkip;

        // RFC 7845 section 5.1: the identification header, alone on the first page.
        Span<byte> head = Body(19);
        "OpusHead"u8.CopyTo(head);
        head[8] = 1;
        head[9] = (byte)channels;
        BinaryPrimitives.WriteUInt16LittleEndian(head[10..], (ushort)preSkip);
        BinaryPrimitives.WriteUInt32LittleEndian(head[12..], (uint)inputSampleRate);
        BinaryPrimitives.WriteInt16LittleEndian(head[16..], 0);
        head[18] = 0;
        AddLacing(19);
        FlushPage(BeginningOfStream, granule: 0);

        // Section 5.2: the comment header, ending on its own page; no user comments.
        int tagsLength = 8 + 4 + vendorLength + 4;
        Span<byte> tags = Body(tagsLength);
        "OpusTags"u8.CopyTo(tags);
        BinaryPrimitives.WriteUInt32LittleEndian(tags[8..], (uint)vendorLength);
        _ = Encoding.UTF8.GetBytes(vendor, tags[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(tags[(12 + vendorLength)..], 0);
        AddLacing(tagsLength);
        FlushPage(0, granule: 0);
    }

    /// <summary>The pre-skip in the identification header.</summary>
    public int PreSkip { get; }

    /// <summary>
    /// Space for the next packet, inside the page being built: pass it to <see cref="OpusEncoder.Encode(ReadOnlySpan{float}, Span{byte})"/>
    /// and then call <see cref="Advance"/> with the length it returned.
    /// </summary>
    public Span<byte> GetPacketSpan()
    {
        ObjectDisposedException.ThrowIf(_finished || _page is null, this);
        if (_segmentCount + MaxPacketSegments > MaxSegments || _bodyLength + OpusPacket.MaxFrameBytes > BodyCapacity)
        {
            FlushPage(0, _granule);
        }

        return _page.AsSpan(MaxHeader + _bodyLength, OpusPacket.MaxFrameBytes);
    }

    /// <summary>Commits the <paramref name="packetLength"/> bytes written into <see cref="GetPacketSpan"/> as one packet.</summary>
    public void Advance(int packetLength)
    {
        ObjectDisposedException.ThrowIf(_finished || _page is null, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(packetLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(packetLength, OpusPacket.MaxFrameBytes);
        _granule += OpusPacket.GetSampleCount(_page.AsSpan(MaxHeader + _bodyLength, packetLength), 48_000);
        _bodyLength += packetLength;
        AddLacing(packetLength);
        if (_bodyLength >= PageTarget)
        {
            FlushPage(0, _granule);
        }
    }

    /// <summary>Adds a packet that is already encoded elsewhere; it is copied once, into the page.</summary>
    public void WritePacket(ReadOnlySpan<byte> packet)
    {
        ArgumentOutOfRangeException.ThrowIfZero(packet.Length, nameof(packet));
        packet.CopyTo(GetPacketSpan());
        Advance(packet.Length);
    }

    /// <summary>
    /// Writes the last page, marked end of stream. Its granule position ends playback after
    /// <paramref name="sampleCount48k"/> samples (the original length at 48 kHz), trimming the encoder's padding in the
    /// final packet (RFC 7845 section 4.4); null keeps every decoded sample.
    /// </summary>
    public void Finish(long? sampleCount48k = null)
    {
        if (_finished || _page is null)
        {
            return;
        }

        long end = sampleCount48k is { } count ? Math.Min(_granule, PreSkip + count) : _granule;
        FlushPage(EndOfStream, end, force: true);
        _finished = true;
    }

    /// <summary>Returns the page buffer to the pool. Call <see cref="Finish"/> first to end the stream.</summary>
    public void Dispose()
    {
        byte[]? page = Interlocked.Exchange(ref _page, null);
        if (page is not null)
        {
            ArrayPool<byte>.Shared.Return(page);
        }
    }

    private Span<byte> Body(int length)
    {
        Span<byte> body = _page.AsSpan(MaxHeader + _bodyLength, length);
        _bodyLength += length;
        return body;
    }

    // Lacing: 255 for each full segment, then the remainder; a packet that is a multiple of 255 ends with 0. The values
    // are kept just before the body; FlushPage writes the fixed header in front of them.
    private void AddLacing(int length)
    {
        Span<byte> lacing = _page.AsSpan(27, MaxSegments);
        for (; length >= 255; length -= 255)
        {
            lacing[_segmentCount++] = 255;
        }

        lacing[_segmentCount++] = (byte)length;
    }

    private void FlushPage(byte flags, long granule, bool force = false)
    {
        if (_segmentCount == 0 && !force)
        {
            return;
        }

        // The header and lacing go right before the body, so the page is one contiguous run with no gap.
        byte[] page = _page!;
        int start = MaxHeader - 27 - _segmentCount;
        if (start != 0)
        {
            page.AsSpan(27, _segmentCount).CopyTo(page.AsSpan(start + 27));
        }

        Span<byte> pageSpan = page.AsSpan(start, 27 + _segmentCount + _bodyLength);
        "OggS"u8.CopyTo(pageSpan);
        pageSpan[4] = 0;
        pageSpan[5] = flags;
        BinaryPrimitives.WriteInt64LittleEndian(pageSpan[6..], granule);
        BinaryPrimitives.WriteUInt32LittleEndian(pageSpan[14..], _serial);
        BinaryPrimitives.WriteUInt32LittleEndian(pageSpan[18..], _sequence++);
        BinaryPrimitives.WriteUInt32LittleEndian(pageSpan[22..], 0);
        pageSpan[26] = (byte)_segmentCount;
        BinaryPrimitives.WriteUInt32LittleEndian(pageSpan[22..], OggCrc.Compute(pageSpan));
        _output.Write(pageSpan);
        _segmentCount = 0;
        _bodyLength = 0;
    }
}

/// <summary>
/// The Ogg page checksum (RFC 3533 section 6): CRC-32 with polynomial 0x04C11DB7, initial value 0, no reflection and no
/// final XOR, eight bytes per step (slicing-by-8).
/// </summary>
internal static class OggCrc
{
    private static readonly uint[] _tables = BuildTables();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<uint> t = _tables;
        uint crc = 0;
        while (data.Length >= 8)
        {
            uint high = crc ^ BinaryPrimitives.ReadUInt32BigEndian(data);
            uint low = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
            crc = t[(7 * 256) + (int)(high >> 24)] ^ t[(6 * 256) + (int)((high >> 16) & 0xFF)]
                ^ t[(5 * 256) + (int)((high >> 8) & 0xFF)] ^ t[(4 * 256) + (int)(high & 0xFF)]
                ^ t[(3 * 256) + (int)(low >> 24)] ^ t[(2 * 256) + (int)((low >> 16) & 0xFF)]
                ^ t[256 + (int)((low >> 8) & 0xFF)] ^ t[(int)(low & 0xFF)];
            data = data[8..];
        }

        foreach (byte value in data)
        {
            crc = (crc << 8) ^ t[(int)((crc >> 24) ^ value)];
        }

        return crc;
    }

    private static uint[] BuildTables()
    {
        uint[] tables = new uint[8 * 256];
        for (uint i = 0; i < 256; i++)
        {
            uint r = i << 24;
            for (int bit = 0; bit < 8; bit++)
            {
                r = (r & 0x80000000) != 0 ? (r << 1) ^ 0x04C11DB7 : r << 1;
            }

            tables[i] = r;
        }

        for (int k = 1; k < 8; k++)
        {
            for (int i = 0; i < 256; i++)
            {
                uint previous = tables[((k - 1) * 256) + i];
                tables[(k * 256) + i] = (previous << 8) ^ tables[(int)(previous >> 24)];
            }
        }

        return tables;
    }
}
