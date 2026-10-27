using Opus.Interop;

// Encodes and decodes a second of a stereo tone, as a Native AOT program, and fails unless the decoder
// reproduces the encoder's range coder state for every packet: what CI runs to show the bindings work
// compiled ahead of time on each platform.

const int Rate = 48_000;
const int Frame = 960;
using OpusEncoder encoder = new(Rate, 2, OpusApplication.Audio) { Bitrate = 96_000 };
using OpusDecoder decoder = new(Rate, 2);
float[] pcm = new float[Frame * 2];
float[] decoded = new float[Frame * 2];
byte[] packet = new byte[OpusPacket.MaxFrameBytes];
for (int f = 0; f < 50; f++)
{
    for (int i = 0; i < Frame; i++)
    {
        float value = (float)(0.4 * Math.Sin(2 * Math.PI * 440 * ((f * Frame) + i) / Rate));
        pcm[i * 2] = value;
        pcm[(i * 2) + 1] = value;
    }

    int length = encoder.Encode(pcm, packet);
    if (
        decoder.Decode(packet.AsSpan(0, length), decoded) != Frame
        || decoder.FinalRange != encoder.FinalRange
    )
    {
        Console.Error.WriteLine($"Frame {f} did not decode as it was encoded.");
        return 1;
    }
}

Console.WriteLine($"{OpusLibrary.Version}: 50 frames encoded and decoded.");
return 0;
