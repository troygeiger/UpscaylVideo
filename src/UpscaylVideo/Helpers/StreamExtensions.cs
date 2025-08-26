using System;
using System.IO;
using System.Threading.Tasks;

namespace UpscaylVideo.Helpers;

public static class StreamExtensions
{
    public static byte[] PngEndSequence = [0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];
    public static byte[] JpegEndSequence = [0xFF, 0xD9];
    
    public static MemoryStream ReadToDelimiter(this Stream stream, Memory<byte> delimiter)
    {
        Span<byte> delimiterSpan = delimiter.Span;
        var resultStream = new MemoryStream();
        int current;
        int matchIndex = 0;
        
        while ((current = stream.ReadByte()) > -1)
        {
            resultStream.WriteByte((byte)current);
            if (delimiterSpan[matchIndex] == current)
            {
                matchIndex++;
            }
            else
            {
                matchIndex = delimiterSpan[0] == current ? 1 : 0;
            }

            if (matchIndex == delimiterSpan.Length)
            {
                resultStream.Position = 0;
                return resultStream;
            }
            
        }

        // Ensure callers always receive a stream positioned at the beginning
        resultStream.Position = 0;
        return resultStream;
    }
    
    public static Task<MemoryStream> ReadToDelimiterAsync(this Stream stream, Memory<byte> delimiter) 
        => Task.Run(() => ReadToDelimiter(stream, delimiter));

    public static MemoryStream ReadNextPng(this Stream stream)
    {
        var result = new MemoryStream();
        ReadOnlySpan<byte> sig = stackalloc byte[8] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

        // Scan for PNG signature without emitting bytes until full match
        Span<byte> sigBuf = stackalloc byte[8];
        int match = 0;
        while (match < sig.Length)
        {
            int b = stream.ReadByte();
            if (b < 0)
            {
                result.Position = 0;
                return result;
            }
            byte bb = (byte)b;
            if (bb == sig[match])
            {
                sigBuf[match++] = bb;
            }
            else if (bb == sig[0])
            {
                // Overlap: start new match from position 1
                sigBuf[0] = bb;
                match = 1;
            }
            else
            {
                // Reset
                match = 0;
            }
        }
        // Write the matched signature
        result.Write(sigBuf);

        // Read chunks until IEND
        Span<byte> type = stackalloc byte[4];
        while (true)
        {
            // Read 4-byte length (big-endian)
            int l0 = stream.ReadByte(); if (l0 < 0) { result.Position = 0; return result; }
            int l1 = stream.ReadByte(); if (l1 < 0) { result.Position = 0; return result; }
            int l2 = stream.ReadByte(); if (l2 < 0) { result.Position = 0; return result; }
            int l3 = stream.ReadByte(); if (l3 < 0) { result.Position = 0; return result; }
            result.WriteByte((byte)l0); result.WriteByte((byte)l1); result.WriteByte((byte)l2); result.WriteByte((byte)l3);
            int length = (l0 << 24) | (l1 << 16) | (l2 << 8) | l3;
            if (length < 0) { result.Position = 0; return result; }

            // Read 4-byte type
            for (int i = 0; i < 4; i++)
            {
                int tb = stream.ReadByte(); if (tb < 0) { result.Position = 0; return result; }
                type[i] = (byte)tb;
                result.WriteByte((byte)tb);
            }

            // Read data
            if (length > 0)
            {
                CopyExactly(stream, result, length);
            }

            // Read 4-byte CRC
            for (int i = 0; i < 4; i++)
            {
                int cb = stream.ReadByte(); if (cb < 0) { result.Position = 0; return result; }
                result.WriteByte((byte)cb);
            }

            // If type == IEND, we're done
            if (type[0] == (byte)'I' && type[1] == (byte)'E' && type[2] == (byte)'N' && type[3] == (byte)'D')
            {
                result.Position = 0;
                return result;
            }
        }
    }
    
    public static Task<MemoryStream> ReadNextPngAsync(this Stream stream) => Task.Run(() => ReadNextPng(stream)); 

    public static MemoryStream ReadNextJpeg(this Stream stream)
    {
        var result = new MemoryStream();

        int ReadByteOrReturn()
        {
            int b = stream.ReadByte();
            if (b < 0)
            {
                result.Position = 0;
                return -1;
            }
            result.WriteByte((byte)b);
            return b;
        }

        // Find SOI (0xFF, 0xD8)
        int prev = -1;
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0)
            {
                result.Position = 0;
                return result;
            }
            if (prev == 0xFF && b == 0xD8)
            {
                result.WriteByte(0xFF);
                result.WriteByte(0xD8);
                break;
            }
            prev = b;
        }

        int pendingMarker = -1;

        while (true)
        {
            int marker;
            if (pendingMarker >= 0)
            {
                // We already consumed the marker prefix and code from entropy loop
                marker = pendingMarker;
                pendingMarker = -1;
            }
            else
            {
                // Read marker prefix 0xFF (skip fill 0xFFs)
                int b;
                do
                {
                    b = ReadByteOrReturn(); if (b < 0) return result;
                } while (b != 0xFF);

                // Read marker code (skip fill 0xFFs)
                do
                {
                    marker = ReadByteOrReturn(); if (marker < 0) return result;
                } while (marker == 0xFF);
            }

            if (marker == 0xD9)
            {
                // EOI
                result.Position = 0;
                return result;
            }

            // Stand-alone markers without length: RST0-7 and TEM (0x01)
            if ((marker >= 0xD0 && marker <= 0xD7) || marker == 0x01)
            {
                continue;
            }

            if (marker == 0xDA)
            {
                // SOS: read its length and payload, then entropy-coded data until next marker
                int l1 = ReadByteOrReturn(); if (l1 < 0) return result;
                int l2 = ReadByteOrReturn(); if (l2 < 0) return result;
                int segLen = (l1 << 8) | l2;
                int toCopy = segLen - 2;
                if (toCopy < 0) { result.Position = 0; return result; }
                CopyExactly(stream, result, toCopy);

                // Entropy-coded data until a marker that is not stuffed (0x00) or restart (D0-D7)
                while (true)
                {
                    int d = stream.ReadByte();
                    if (d < 0)
                    {
                        result.Position = 0;
                        return result;
                    }
                    result.WriteByte((byte)d);
                    if (d != 0xFF) continue;

                    // Potential marker: read next byte
                    int n = stream.ReadByte();
                    if (n < 0)
                    {
                        result.Position = 0;
                        return result;
                    }
                    result.WriteByte((byte)n);

                    if (n == 0x00)
                    {
                        // Stuffed 0xFF
                        continue;
                    }

                    if (n >= 0xD0 && n <= 0xD7)
                    {
                        // Restart marker
                        continue;
                    }

                    // We've hit a real marker; handle it in the outer loop next
                    pendingMarker = n;
                    break;
                }
                continue;
            }
            else
            {
                // Regular segment with 2-byte big-endian length, then that many-2 bytes of data
                int l1 = ReadByteOrReturn(); if (l1 < 0) return result;
                int l2 = ReadByteOrReturn(); if (l2 < 0) return result;
                int segLen = (l1 << 8) | l2;
                int toCopy = segLen - 2;
                if (toCopy < 0) { result.Position = 0; return result; }
                CopyExactly(stream, result, toCopy);
            }
        }
    }

    public static Task<MemoryStream> ReadNextJpegAsync(this Stream stream) => Task.Run(() => ReadNextJpeg(stream));

    public static MemoryStream ReadNextImage(this Stream stream, string imageFormat)
    {
        var fmt = string.IsNullOrWhiteSpace(imageFormat) ? "png" : imageFormat.ToLowerInvariant();
        return fmt switch
        {
            "png" => stream.ReadNextPng(),
            "jpg" => stream.ReadNextJpeg(),
            "jpeg" => stream.ReadNextJpeg(),
            _ => stream.ReadNextPng()
        };
    }

    public static Task<MemoryStream> ReadNextImageAsync(this Stream stream, string imageFormat)
        => Task.Run(() => ReadNextImage(stream, imageFormat));


    private static void CopyExactly(Stream input, Stream output, long bytesToCopy)
    {
        byte[] buffer = new byte[8192];
        long remaining = bytesToCopy;
        while (remaining > 0)
        {
            int toRead = remaining > buffer.Length ? buffer.Length : (int)remaining;
            int read = input.Read(buffer, 0, toRead);
            if (read <= 0)
            {
                break; // End of stream; return what we have
            }
            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }
}