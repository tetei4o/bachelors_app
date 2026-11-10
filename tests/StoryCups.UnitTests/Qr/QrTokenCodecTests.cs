using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using StoryCups.Domain.Qr;

namespace StoryCups.UnitTests.Qr;

public sealed class QrTokenCodecTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private static readonly Guid PublicId = new("7f3c2a10-5b6d-4e8f-9a1b-2c3d4e5f6071");

    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    private QrTokenCodec Codec(byte keyId = 1) => new(new Dictionary<byte, byte[]> { [keyId] = key }, keyId);

    [Fact]
    public void Issue_ProducesNinetyNineBase64UrlCharacters()
    {
        var issued = Codec().Issue(PublicId, Now, Lifetime);

        Assert.Equal(QrTokenFormat.EncodedLength, issued.Token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", issued.Token);
        Assert.Equal(QrTokenFormat.Length, Base64Url.DecodeFromChars(issued.Token).Length);
    }

    [Fact]
    public void Issue_WritesFieldsInTheDocumentedLayout()
    {
        var nonce = RandomNumberGenerator.GetBytes(QrTokenFormat.NonceLength);

        var bytes = Base64Url.DecodeFromChars(Codec(keyId: 7).Issue(PublicId, Now, Lifetime, nonce).Token);

        Assert.Equal(74, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(7, bytes[1]);
        Assert.Equal(PublicId, new Guid(bytes.AsSpan(2, 16), bigEndian: true));
        Assert.Equal(Now.ToUnixTimeSeconds(), BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(18, 4)));
        Assert.Equal(Now.ToUnixTimeSeconds() + 60, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(22, 4)));
        Assert.Equal(nonce, bytes[26..42]);

        // Подписът е HMAC-SHA256 върху всички предходни байтове, изчислен тук независимо от кодека.
        Assert.Equal(HMACSHA256.HashData(key, bytes.AsSpan(0, 42)), bytes[42..74]);
    }

    [Fact]
    public void Validate_FreshToken_ReturnsThePayload()
    {
        var codec = Codec();
        var issued = codec.Issue(PublicId, Now, Lifetime);

        var result = codec.Validate(issued.Token, Now.AddSeconds(30));

        Assert.Equal(QrTokenStatus.Valid, result.Status);
        Assert.Equal(PublicId, result.Payload!.PublicId);
        Assert.Equal(Now, result.Payload.IssuedAt);
        Assert.Equal(Now + Lifetime, result.Payload.ExpiresAt);
        Assert.Equal(issued.Payload.Nonce, result.Payload.Nonce);
        Assert.Equal(1, result.Payload.KeyId);
    }

    [Fact]
    public void Issue_TruncatesTimeToWholeSeconds()
    {
        var issued = Codec().Issue(PublicId, Now.AddMilliseconds(900), Lifetime);

        Assert.Equal(Now, issued.Payload.IssuedAt);
        Assert.Equal(Now + Lifetime, issued.Payload.ExpiresAt);
    }

    [Fact]
    public void Issue_TwiceForTheSameCustomer_GivesDifferentNonceAndToken()
    {
        var codec = Codec();

        var first = codec.Issue(PublicId, Now, Lifetime);
        var second = codec.Issue(PublicId, Now, Lifetime);

        Assert.NotEqual(first.Payload.Nonce, second.Payload.Nonce);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public void Validate_AnySingleFlippedBit_IsRejected()
    {
        var codec = Codec();
        var original = Base64Url.DecodeFromChars(codec.Issue(PublicId, Now, Lifetime).Token);

        for (var index = 0; index < original.Length; index++)
        {
            var tampered = (byte[])original.Clone();
            tampered[index] ^= 0x01;

            var result = codec.Validate(Base64Url.EncodeToString(tampered), Now);

            var expected = index switch
            {
                QrTokenFormat.VersionOffset => QrTokenStatus.Malformed,
                QrTokenFormat.KeyIdOffset => QrTokenStatus.UnknownKey,
                _ => QrTokenStatus.BadSignature,
            };
            Assert.True(expected == result.Status, $"Байт {index}: очаквано {expected}, получено {result.Status}.");
            Assert.Null(result.Payload);
        }
    }

    [Fact]
    public void Validate_ExtendedExpiryWithoutTheKey_IsRejected()
    {
        // Нападател удължава срока на заснет код: полето exp е под подписа, затова промяната се хваща.
        var codec = Codec();
        var bytes = Base64Url.DecodeFromChars(codec.Issue(PublicId, Now, Lifetime).Token);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(QrTokenFormat.ExpiresAtOffset), (uint)Now.AddDays(1).ToUnixTimeSeconds());

        var result = codec.Validate(Base64Url.EncodeToString(bytes), Now.AddMinutes(5));

        Assert.Equal(QrTokenStatus.BadSignature, result.Status);
    }

    [Theory]
    [InlineData(59, QrTokenStatus.Valid)]
    [InlineData(60, QrTokenStatus.Expired)]
    [InlineData(61, QrTokenStatus.Expired)]
    public void Validate_ExpiresExactlyAtExp(int secondsAfterIssue, QrTokenStatus expected)
    {
        var codec = Codec();
        var issued = codec.Issue(PublicId, Now, Lifetime);

        Assert.Equal(expected, codec.Validate(issued.Token, Now.AddSeconds(secondsAfterIssue)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("кратък")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public void Validate_WrongLengthOrEncoding_IsMalformed(string? token)
    {
        Assert.Equal(QrTokenStatus.Malformed, Codec().Validate(token, Now).Status);
    }

    [Fact]
    public void Validate_TokenSignedWithAnotherKey_IsBadSignature()
    {
        var foreign = new QrTokenCodec(new Dictionary<byte, byte[]> { [1] = RandomNumberGenerator.GetBytes(32) }, 1);

        var result = Codec().Validate(foreign.Issue(PublicId, Now, Lifetime).Token, Now);

        Assert.Equal(QrTokenStatus.BadSignature, result.Status);
    }

    [Fact]
    public void Rotation_OldKeyIsAcceptedUntilItIsRemoved()
    {
        var oldKey = RandomNumberGenerator.GetBytes(32);
        var newKey = RandomNumberGenerator.GetBytes(32);
        var before = new QrTokenCodec(new Dictionary<byte, byte[]> { [1] = oldKey }, 1);
        var during = new QrTokenCodec(new Dictionary<byte, byte[]> { [1] = oldKey, [2] = newKey }, 2);
        var after = new QrTokenCodec(new Dictionary<byte, byte[]> { [2] = newKey }, 2);
        var issuedBefore = before.Issue(PublicId, Now, Lifetime).Token;

        // По време на ротацията новите токени са с новия ключ, а старите още се приемат.
        Assert.Equal(2, during.Validate(during.Issue(PublicId, Now, Lifetime).Token, Now).Payload!.KeyId);
        Assert.Equal(QrTokenStatus.Valid, during.Validate(issuedBefore, Now).Status);

        // След един живот на токена старият ключ се маха от конфигурацията.
        Assert.Equal(QrTokenStatus.UnknownKey, after.Validate(issuedBefore, Now).Status);
    }

    [Fact]
    public void Constructor_RejectsShortKeyAndMissingActiveKey()
    {
        Assert.Throws<ArgumentException>(() => new QrTokenCodec(new Dictionary<byte, byte[]> { [1] = new byte[31] }, 1));
        Assert.Throws<ArgumentException>(() => new QrTokenCodec(new Dictionary<byte, byte[]> { [1] = key }, 2));
    }

    [Fact]
    public void Issue_RejectsNonPositiveLifetimeAndWrongNonceLength()
    {
        var codec = Codec();

        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Issue(PublicId, Now, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => codec.Issue(PublicId, Now, Lifetime, new byte[15]));
    }
}
