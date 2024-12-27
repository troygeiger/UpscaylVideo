using System;
using System.IO;
using System.Threading.Tasks;

namespace UpscaylVideo.Helpers;

public static class StreamExtensions
{
    public static byte[] PngEndSequence = [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];
    
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
                matchIndex = 0;
            }

            if (matchIndex == delimiterSpan.Length)
            {
                resultStream.Position = 0;
                return resultStream;
            }
            
        }

        return resultStream;
    }
    
    public static Task<MemoryStream> ReadToDelimiterAsync(this Stream stream, Memory<byte> delimiter) 
        => Task.Run(() => ReadToDelimiter(stream, delimiter));

    public static MemoryStream ReadNextPng(this Stream stream)
    {
        return ReadToDelimiter(stream, PngEndSequence);
    }
    
    public static Task<MemoryStream> ReadNextPngAsync(this Stream stream) => Task.Run(() => ReadNextPng(stream)); 
}