using System.Buffers;
using System.Buffers.Binary;

namespace LibOpus.Interop.Tests;

[TestClass]
public sealed class OggOpusWriterTests
{
    private const int Rate = 48_000;
    private const int Frame = 960;

    [TestMethod]
    public void Write_OneSecondOfSpeech_ProducesValidPagesThatDecode()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Voip) { Bitrate = 32_000 };
        ArrayBufferWriter<byte> output = new();
        using OggOpusWriter writer = new(output, channels: 1, inputSampleRate: 24_000, preSkip: encoder.Lookahead, serial: 7);
        float[] pcm = new float[Frame];
        for (int f = 0; f < 50; f++)
        {
            for (int i = 0; i < Frame; i++)
            {
                pcm[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 300 * ((f * Frame) + i) / Rate));
            }

            writer.Advance(encoder.Encode(pcm, writer.GetPacketSpan()));
        }

        writer.Finish(sampleCount48k: (50 * Frame) - 100);
        List<Page> pages = ReadPages(output.WrittenSpan.ToArray());

        Assert.IsGreaterThanOrEqualTo(3, pages.Count);
        Assert.AreEqual(0x02, pages[0].Flags, "the first page begins the stream");
        Assert.AreEqual(0x04, pages[^1].Flags, "the last page ends it");
        Assert.IsTrue(pages.All(static p => p.Serial == 7));
        Assert.IsTrue(pages.Select(static (p, i) => p.Sequence == i).All(static ok => ok), "sequence numbers count up from 0");

        byte[] head = pages[0].Packets.Single();
        Assert.AreEqual("OpusHead", System.Text.Encoding.ASCII.GetString(head, 0, 8));
        Assert.AreEqual(1, head[8]);
        Assert.AreEqual(1, head[9]);
        Assert.AreEqual(encoder.Lookahead, BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(10)));
        Assert.AreEqual(24_000u, BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(12)));
        Assert.AreEqual("OpusTags", System.Text.Encoding.ASCII.GetString(pages[1].Packets.Single(), 0, 8));
        Assert.AreEqual(encoder.Lookahead + (50 * Frame) - 100, pages[^1].Granule, "the end trims the padding");

        using OpusDecoder decoder = new(Rate, 1);
        float[] decoded = new float[Frame * 3];
        int total = pages.Skip(2).SelectMany(static p => p.Packets).Sum(p => decoder.Decode(p, decoded));
        Assert.AreEqual(50 * Frame, total);
    }

    [TestMethod]
    public void WritePacket_LargePacketOfExactly255Bytes_EndsItsLacingWithZero()
    {
        ArrayBufferWriter<byte> output = new();
        using OggOpusWriter writer = new(output, 1, Rate, preSkip: 312, serial: 1);
        byte[] packet = new byte[255];
        packet[0] = 0xF8; // CELT FB 20 ms, one frame: a valid TOC byte.
        writer.WritePacket(packet);
        writer.Finish();

        Page last = ReadPages(output.WrittenSpan.ToArray())[^1];
        CollectionAssert.AreEqual(new byte[] { 255, 0 }, last.Lacing);
        Assert.AreEqual(255, last.Packets.Single().Length);
    }

    [TestMethod]
    public void Write_ManyPackets_SpansPagesWithCountingSequenceAndRisingGranules()
    {
        using OpusEncoder encoder = new(Rate, 2, OpusApplication.Audio) { Bitrate = 256_000 };
        ArrayBufferWriter<byte> output = new();
        using OggOpusWriter writer = new(output, 2, Rate, preSkip: encoder.Lookahead, serial: 3);
        float[] pcm = new float[Frame * 2];
        Random noise = new(1);
        for (int f = 0; f < 500; f++)
        {
            for (int i = 0; i < pcm.Length; i++)
            {
                pcm[i] = (float)((noise.NextDouble() * 2) - 1) * 0.5f;
            }

            writer.Advance(encoder.Encode(pcm, writer.GetPacketSpan()));
        }

        writer.Finish();
        List<Page> pages = ReadPages(output.WrittenSpan.ToArray());

        Assert.IsGreaterThan(10, pages.Count, "loud noise at 256 kbit/s fills many pages");
        Assert.AreEqual(500, pages.Skip(2).Sum(static p => p.Packets.Count));
        long previous = 0;
        foreach (Page page in pages.Skip(2))
        {
            Assert.IsGreaterThan(previous, page.Granule);
            previous = page.Granule;
        }

        Assert.AreEqual(encoder.Lookahead + (500L * Frame), pages[^1].Granule);
    }

    [TestMethod]
    public void Advance_SteadyState_AllocatesNothing()
    {
        using OpusEncoder encoder = new(Rate, 1, OpusApplication.Voip) { Bitrate = 32_000 };
        ArrayBufferWriter<byte> output = new(1 << 20);
        using OggOpusWriter writer = new(output, 1, Rate, preSkip: encoder.Lookahead);
        float[] pcm = new float[Frame];
        for (int i = 0; i < pcm.Length; i++)
        {
            pcm[i] = (float)Math.Sin(i * 0.05) * 0.3f;
        }

        for (int f = 0; f < 20; f++)
        {
            writer.Advance(encoder.Encode(pcm, writer.GetPacketSpan()));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int f = 0; f < 500; f++)
        {
            writer.Advance(encoder.Encode(pcm, writer.GetPacketSpan()));
        }

        Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before, "bytes allocated while writing 500 packets");
    }

    private sealed record Page(byte Flags, long Granule, uint Serial, uint Sequence, byte[] Lacing, List<byte[]> Packets);

    // Reads pages back, checking each page's CRC the way RFC 3533 defines it.
    private static List<Page> ReadPages(byte[] stream)
    {
        List<Page> pages = [];
        int offset = 0;
        while (offset < stream.Length)
        {
            Assert.AreEqual("OggS", System.Text.Encoding.ASCII.GetString(stream, offset, 4));
            int segments = stream[offset + 26];
            byte[] lacing = stream.AsSpan(offset + 27, segments).ToArray();
            int bodyLength = lacing.Sum(static l => l);
            int pageLength = 27 + segments + bodyLength;
            byte[] page = stream.AsSpan(offset, pageLength).ToArray();
            uint stored = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(22));
            page.AsSpan(22, 4).Clear();
            Assert.AreEqual(stored, Crc(page), "page CRC");

            List<byte[]> packets = [];
            List<byte> current = [];
            int body = offset + 27 + segments;
            foreach (byte l in lacing)
            {
                current.AddRange(stream.AsSpan(body, l).ToArray());
                body += l;
                if (l < 255)
                {
                    packets.Add([.. current]);
                    current.Clear();
                }
            }

            pages.Add(new Page(
                stream[offset + 5],
                BinaryPrimitives.ReadInt64LittleEndian(stream.AsSpan(offset + 6)),
                BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan(offset + 14)),
                BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan(offset + 18)),
                lacing,
                packets));
            offset += pageLength;
        }

        return pages;
    }

    private static uint Crc(byte[] data)
    {
        uint crc = 0;
        foreach (byte value in data)
        {
            crc ^= (uint)value << 24;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
            }
        }

        return crc;
    }
}
