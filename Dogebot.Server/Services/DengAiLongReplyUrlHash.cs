using System.Buffers.Text;

namespace Dogebot.Server.Services;

internal static class DengAiLongReplyUrlHash
{
    public const int ByteLength = 10;

    public static string Create(ReadOnlySpan<byte> contentHashBytes, int sliceIndex) => Base64Url.EncodeToString(contentHashBytes.Slice(sliceIndex * ByteLength, ByteLength));
}
