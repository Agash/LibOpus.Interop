# LibOpus.Interop

.NET bindings for [libopus](https://opus-codec.org/) 1.6: an Opus encoder and decoder for .NET 11 with
every control typed, over complete source-generated bindings to the library. libopus ships in
the package for Windows, Linux and macOS on x64 and arm64.

- **Encode and decode** float, 16-bit and 24-bit PCM at 8 to 48 kHz, mono or stereo, in 2.5 to 120 ms
  frames.
- **Every control typed**: bitrate (changed between frames), complexity, variable or constant bitrate,
  in-band forward error correction and the loss it is tuned for, discontinuous transmission, signal
  type, bandwidth, frame duration, prediction, phase inversion, gain.
- **Loss handling**: concealment of a lost packet, and recovery of it from the next packet's FEC.
- **Packet inspection** from the table of contents: bandwidth, channels, frames, duration, whether FEC
  is carried.
- **Allocation-free** on every encode and decode path; spans in, counts out.
- **Native AOT compatible**, with runtime marshalling disabled.

## Getting started

```bash
dotnet add package LibOpus.Interop
```

```csharp
using LibOpus.Interop;

using var encoder = new OpusEncoder(48_000, channels: 2, OpusApplication.Audio)
{
    Bitrate = 96_000,
    InbandFec = OpusInbandFec.On,
    PacketLossPercentage = 10,
};
using var decoder = new OpusDecoder(48_000, channels: 2);

float[] pcm = new float[960 * 2]; // 20 ms, interleaved
byte[] packet = new byte[OpusPacket.MaxFrameBytes];
int length = encoder.Encode(pcm, packet);

float[] decoded = new float[OpusPacket.MaxSamplesPerChannel * 2];
int samples = decoder.Decode(packet.AsSpan(0, length), decoded);
```

A packet that never arrived is concealed by decoding an empty one, or recovered from the forward error
correction the next packet carries:

```csharp
int recovered = decoder.DecodeForwardErrorCorrection(nextPacket, decoded.AsSpan(0, 960 * 2));
```

The bindings themselves are in `LibOpus.Interop.Native` under libopus's own names (`Opus.opus_encode`,
`Opus.OPUS_SET_BITRATE_REQUEST`), for anything the managed layer does not cover yet; the controls go
through `Opus.opus_interop_*_ctl*`, which are fixed-signature forms of libopus's variadic ones.

## Building

See [CONTRIBUTING.md](CONTRIBUTING.md): `cmake -P eng/build-native.cmake` builds libopus from the pinned source,
then `dotnet build` and `dotnet test`.

## License

MIT for this library. libopus is BSD-3-Clause; its license ships in the package as
`LICENSE.opus.txt`.
