using System.IO;
using UpscaylVideo.Helpers;
using System;

namespace UpscaylVideo.Tests;

public class ImageParseTests
{
    [Fact]
    public void ReadNextPng_ParsesUntilIend_IgnoresPrefixNoise()
    {
        // PNG: signature + IHDR(13 bytes) + IDAT(0) + IEND(0)
        var sig = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };
        byte[] ihdrLen = { 0x00, 0x00, 0x00, 0x0D };
        byte[] ihdrType = { (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        byte[] ihdrData = new byte[13]; // dummy
        byte[] ihdrCrc = { 0, 1, 2, 3 };
        byte[] idatLen = { 0x00, 0x00, 0x00, 0x00 };
        byte[] idatType = { (byte)'I', (byte)'D', (byte)'A', (byte)'T' };
        byte[] idatCrc = { 4, 5, 6, 7 };
        byte[] iendLen = { 0x00, 0x00, 0x00, 0x00 };
        byte[] iendType = { (byte)'I', (byte)'E', (byte)'N', (byte)'D' };
        byte[] iendCrc = { 8, 9, 10, 11 };

        using var ms = new MemoryStream();
        // prefix noise
        ms.Write(new byte[] { 0x00, 0x11, 0x22 });
        ms.Write(sig);
        ms.Write(ihdrLen); ms.Write(ihdrType); ms.Write(ihdrData); ms.Write(ihdrCrc);
        ms.Write(idatLen); ms.Write(idatType); /* no data */ ms.Write(idatCrc);
        ms.Write(iendLen); ms.Write(iendType); /* no data */ ms.Write(iendCrc);
        ms.Position = 0;

        var image = ms.ReadNextPng();
        Assert.NotNull(image);
        Assert.Equal(0, image.Position);
        var outBytes = image.ToArray();

        // Should start with signature and end with IEND+CRC
        Assert.True(outBytes.AsSpan(0, sig.Length).SequenceEqual(sig));
        Assert.True(outBytes.AsSpan(outBytes.Length - iendCrc.Length, iendCrc.Length).SequenceEqual(iendCrc));
    }

    [Fact]
    public void ReadNextJpeg_ParsesSegmentsAndEntropyUntilEoi()
    {
        using var ms = new MemoryStream();
        // prefix noise
        ms.Write(new byte[] { 0x12, 0x34 });
        // SOI
        ms.WriteByte(0xFF); ms.WriteByte(0xD8);
        // APP0 (FFE0) length 0x0010 with 14 bytes payload
        ms.WriteByte(0xFF); ms.WriteByte(0xE0);
        ms.WriteByte(0x00); ms.WriteByte(0x10);
        ms.Write(new byte[14]);
        // DQT (FFDB) length 0x0004 with 2 bytes
        ms.WriteByte(0xFF); ms.WriteByte(0xDB);
        ms.WriteByte(0x00); ms.WriteByte(0x04);
        ms.Write(new byte[2]);
        // SOS (FFDA) length 0x0006 with 4 bytes params
        ms.WriteByte(0xFF); ms.WriteByte(0xDA);
        ms.WriteByte(0x00); ms.WriteByte(0x06);
        ms.Write(new byte[4]);
        // Entropy-coded data: some bytes, a stuffed 0xFF, a restart, then EOI marker
        ms.WriteByte(0x01);
        ms.WriteByte(0xFF); ms.WriteByte(0x00); // stuffed
        ms.WriteByte(0x02);
        ms.WriteByte(0xFF); ms.WriteByte(0xD0); // RST0
        ms.WriteByte(0x03);
        ms.WriteByte(0xFF); ms.WriteByte(0xD9); // EOI
        ms.Position = 0;

        var image = ms.ReadNextJpeg();
        Assert.NotNull(image);
        Assert.Equal(0, image.Position);
        var data = image.ToArray();
        // Should begin with SOI and end with EOI
        Assert.True(data.AsSpan(0, 2).SequenceEqual(new byte[] { 0xFF, 0xD8 }));
        Assert.True(data.AsSpan(data.Length - 2, 2).SequenceEqual(new byte[] { 0xFF, 0xD9 }));
    }

    [Fact]
    public void ReadNextWebp_SynchronizesOnRiffAndCopiesBySize()
    {
        using var ms = new MemoryStream();
        // prefix noise
        ms.Write(new byte[] { 0x77, 0x88, 0x99 });
        // RIFF header
        ms.WriteByte((byte)'R'); ms.WriteByte((byte)'I'); ms.WriteByte((byte)'F'); ms.WriteByte((byte)'F');
        // size: 8 (so total size = 16, remaining after 12 header = 4)
        ms.WriteByte(8); ms.WriteByte(0); ms.WriteByte(0); ms.WriteByte(0);
        ms.WriteByte((byte)'W'); ms.WriteByte((byte)'E'); ms.WriteByte((byte)'B'); ms.WriteByte((byte)'P');
        // 4 bytes payload
        ms.Write(new byte[] { 1, 2, 3, 4 });
        ms.Position = 0;

        var image = ms.ReadNextImage("webp");
        Assert.NotNull(image);
        var data = image.ToArray();
        // RIFF
        Assert.Equal((byte)'R', data[0]);
        Assert.Equal((byte)'I', data[1]);
        Assert.Equal((byte)'F', data[2]);
        Assert.Equal((byte)'F', data[3]);
        // WEBP
        Assert.Equal((byte)'W', data[8]);
        Assert.Equal((byte)'E', data[9]);
        Assert.Equal((byte)'B', data[10]);
        Assert.Equal((byte)'P', data[11]);
        Assert.Equal(16, data.Length); // size (8) + 8 = 16 total
        Assert.True(data.AsSpan(data.Length - 4, 4).SequenceEqual(new byte[] { 1, 2, 3, 4 }));
    }
}
