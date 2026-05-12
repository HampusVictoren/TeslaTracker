using System.Buffers.Binary;

namespace TeslaTracker.Infrastructure.Crypto;

/// <summary>
/// Binary frame: [4B nonceLen | nonce | 4B tagLen | tag | ciphertext].
/// Encoding-only — does not perform any encryption itself.
/// </summary>
internal static class EnvelopeCipher
{
    public static byte[] Pack(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> ciphertext)
    {
        var buffer = new byte[4 + nonce.Length + 4 + tag.Length + ciphertext.Length];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32LittleEndian(span, nonce.Length);
        span = span[4..];
        nonce.CopyTo(span);
        span = span[nonce.Length..];

        BinaryPrimitives.WriteInt32LittleEndian(span, tag.Length);
        span = span[4..];
        tag.CopyTo(span);
        span = span[tag.Length..];

        ciphertext.CopyTo(span);
        return buffer;
    }

    public static (ReadOnlyMemory<byte> Nonce, ReadOnlyMemory<byte> Tag, ReadOnlyMemory<byte> Ciphertext) Unpack(ReadOnlyMemory<byte> blob)
    {
        if (blob.Length < 8)
        {
            throw new ArgumentException("Envelope blob är för kort för att innehålla nonce + tag-längder.", nameof(blob));
        }

        var span = blob.Span;
        var offset = 0;

        var nonceLen = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;

        if (nonceLen < 0 || offset + nonceLen > blob.Length)
        {
            throw new ArgumentException("Ogiltig nonce-längd i envelope blob.", nameof(blob));
        }
        var nonce = blob.Slice(offset, nonceLen);
        offset += nonceLen;

        if (offset + 4 > blob.Length)
        {
            throw new ArgumentException("Envelope blob saknar tag-längd.", nameof(blob));
        }
        var tagLen = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;

        if (tagLen < 0 || offset + tagLen > blob.Length)
        {
            throw new ArgumentException("Ogiltig tag-längd i envelope blob.", nameof(blob));
        }
        var tag = blob.Slice(offset, tagLen);
        offset += tagLen;

        var ciphertext = blob[offset..];
        return (nonce, tag, ciphertext);
    }
}
