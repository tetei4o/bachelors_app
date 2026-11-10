using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;
using StoryCups.Contracts.Loyalty;
using StoryCups.Domain.Identity;
using StoryCups.Domain.Qr;
using StoryCups.Infrastructure.Identity;
using StoryCups.Infrastructure.Persistence;
using StoryCups.Infrastructure.Qr;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

/// <summary>
/// QR токен и сканиране през HTTP срещу истински PostgreSQL (ADR-005, ADR-006, ADR-008).
/// </summary>
[Collection(AppCollection.Name)]
public sealed class QrScanApiTests(StoryCupsAppFactory app)
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task QrToken_Customer_GetsTokenThatFitsQrVersion6()
    {
        var customer = await app.NewCustomerAsync(client);

        var response = await client.IssueQrTokenAsync(customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var token = (await response.Content.ReadFromJsonAsync<QrTokenResponse>())!;
        Assert.Equal(QrTokenFormat.EncodedLength, token.Token.Length);
        Assert.Equal(60, token.LifetimeSeconds);
        Assert.Equal(40, token.RefreshAfterSeconds);
        Assert.InRange(token.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(50), DateTimeOffset.UtcNow.AddSeconds(61));

        // Твърдението от ADR-005: 99 символа се побират в QR версия 6 при ниво на корекция M.
        using var generator = new QRCodeGenerator();
        using var qr = generator.CreateQrCode(token.Token, QRCodeGenerator.ECCLevel.M);
        Assert.Equal(6, qr.Version);
    }

    [Fact]
    public async Task QrToken_ContainsPublicIdAndNotTheInternalKey()
    {
        var customer = await app.NewCustomerAsync(client);
        var token = await client.IssueQrTokenOkAsync(customer);

        var publicId = await PublicIdOfAsync(customer);
        var bytes = Base64Url.DecodeFromChars(token.Token);

        Assert.NotEqual(customer.Id, publicId);
        Assert.Equal(publicId, new Guid(bytes.AsSpan(QrTokenFormat.PublicIdOffset, 16), bigEndian: true));
        Assert.Equal(-1, bytes.AsSpan().IndexOf(customer.Id.ToByteArray(bigEndian: true)));
    }

    [Fact]
    public async Task QrToken_AnonymousOrStaff_IsRefused()
    {
        var barista = await app.NewStaffAsync(client);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.IssueQrTokenAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.IssueQrTokenAsync(barista.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Scan_ValidToken_OpensSessionAndShowsOnlyInitials()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var token = await client.IssueQrTokenOkAsync(customer);

        var response = await client.ScanAsync(barista.AccessToken, token.Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var session = (await response.Content.ReadFromJsonAsync<ScanSessionDto>())!;
        Assert.Equal("ТК", session.CustomerInitials);
        Assert.Equal(120, session.LifetimeSeconds);
        Assert.NotEqual(Guid.Empty, session.Id);

        // Баристата не получава име, имейл или идентификатор на клиента (A19).
        Assert.DoesNotContain(customer.Email, body);
        Assert.DoesNotContain("Тест Клиент", body);
        Assert.DoesNotContain(customer.Id.ToString(), body);
    }

    [Fact]
    public async Task Scan_Admin_IsAllowed()
    {
        var customer = await app.NewCustomerAsync(client);
        var admin = await app.NewStaffAsync(client, AppRoles.Admin);
        var token = await client.IssueQrTokenOkAsync(customer);

        Assert.Equal(HttpStatusCode.Created, (await client.ScanAsync(admin.AccessToken, token.Token)).StatusCode);
    }

    [Fact]
    public async Task Scan_SameTokenTwice_SecondIsRejectedAsAlreadyUsed()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var other = await app.NewStaffAsync(client);
        var token = await client.IssueQrTokenOkAsync(customer);
        Assert.Equal(HttpStatusCode.Created, (await client.ScanAsync(barista.AccessToken, token.Token)).StatusCode);

        // Екранна снимка на вече сканиран код: не работи нито при същия, нито при друг бариста.
        var again = await client.ScanAsync(barista.AccessToken, token.Token);
        var elsewhere = await client.ScanAsync(other.AccessToken, token.Token);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.QrAlreadyUsed, await again.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, elsewhere.StatusCode);
    }

    [Fact]
    public async Task Scan_SameTokenConcurrently_ExactlyOneSucceeds()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var token = await client.IssueQrTokenOkAsync(customer);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.ScanAsync(barista.AccessToken, token.Token)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Scan_TwoFreshTokensOfOneCustomer_BothWork()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);

        var first = await client.ScanOkAsync(barista, customer);
        var second = await client.ScanOkAsync(barista, customer);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Scan_TamperedToken_IsInvalid()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var token = (await client.IssueQrTokenOkAsync(customer)).Token;
        var tampered = token[..40] + (token[40] == 'A' ? 'B' : 'A') + token[41..];

        var response = await client.ScanAsync(barista.AccessToken, tampered);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.QrInvalid, await response.ErrorCodeAsync());

        // Неуспешният опит не използва nonce: истинският код още работи.
        Assert.Equal(HttpStatusCode.Created, (await client.ScanAsync(barista.AccessToken, token)).StatusCode);
    }

    [Theory]
    [InlineData("https://example.com/some-other-qr-code")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    public async Task Scan_ForeignQrCode_IsInvalid(string content)
    {
        var barista = await app.NewStaffAsync(client);

        var response = await client.ScanAsync(barista.AccessToken, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.QrInvalid, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Scan_TokenSignedWithAnotherKey_IsInvalid()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var foreign = new QrTokenCodec(new Dictionary<byte, byte[]> { [1] = RandomNumberGenerator.GetBytes(32) }, 1);
        var forged = foreign.Issue(await PublicIdOfAsync(customer), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60)).Token;

        var response = await client.ScanAsync(barista.AccessToken, forged);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.QrInvalid, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Scan_ExpiredToken_IsGone()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);

        // Истински подписан токен, издаден преди две минути: животът му от 60 s е минал.
        var codec = app.Services.GetRequiredService<QrTokenCodec>();
        var expired = codec.Issue(await PublicIdOfAsync(customer), DateTimeOffset.UtcNow.AddMinutes(-2), TimeSpan.FromSeconds(60)).Token;

        var response = await client.ScanAsync(barista.AccessToken, expired);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.QrExpired, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Scan_ByCustomerOrAnonymous_IsRefused()
    {
        var customer = await app.NewCustomerAsync(client);
        var friend = await app.NewCustomerAsync(client);
        var token = (await client.IssueQrTokenOkAsync(customer)).Token;

        Assert.Equal(HttpStatusCode.Forbidden, (await client.ScanAsync(friend.AccessToken, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.ScanAsync(null, token)).StatusCode);
    }

    [Fact]
    public async Task Scan_LockedCustomer_IsRefusedAsInactive()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var token = (await client.IssueQrTokenOkAsync(customer)).Token;

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(customer.Id.ToString());
            Assert.True((await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
        }

        var response = await client.ScanAsync(barista.AccessToken, token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(LoyaltyErrorCodes.CustomerInactive, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Scan_EmptyBody_Returns400WithFieldError()
    {
        var barista = await app.NewStaffAsync(client);

        var response = await client.ScanAsync(barista.AccessToken, string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"Token\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UsedNonceCleaner_DeletesOnlyExpiredNonces()
    {
        var customer = await app.NewCustomerAsync(client);
        var barista = await app.NewStaffAsync(client);
        var fresh = await client.IssueQrTokenOkAsync(customer);
        Assert.Equal(HttpStatusCode.Created, (await client.ScanAsync(barista.AccessToken, fresh.Token)).StatusCode);
        var freshNonce = Base64Url.DecodeFromChars(fresh.Token).AsSpan(QrTokenFormat.NonceOffset, QrTokenFormat.NonceLength).ToArray();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = new QrTokenPayload(1, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-9), RandomNumberGenerator.GetBytes(16));
        db.UsedQrNonces.Add(UsedQrNonce.From(old));
        await db.SaveChangesAsync();

        var deleted = await scope.ServiceProvider.GetRequiredService<UsedNonceCleaner>().DeleteExpiredAsync();

        Assert.True(deleted >= 1);
        Assert.False(await db.UsedQrNonces.AnyAsync(n => n.Nonce == old.Nonce));
        Assert.True(await db.UsedQrNonces.AnyAsync(n => n.Nonce == freshNonce));
    }

    private async Task<Guid> PublicIdOfAsync(TestUser user)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.Where(u => u.Id == user.Id).Select(u => u.PublicId).SingleAsync();
    }
}

public sealed class QrRateLimitedAppFactory : StoryCupsAppFactory
{
    protected override int QrTokensPerMinute => 3;

    protected override int ScansPerMinute => 2;

    protected override int ActionsPerMinute => 2;
}

public sealed class QrRateLimitTests(QrRateLimitedAppFactory app) : IClassFixture<QrRateLimitedAppFactory>
{
    private readonly HttpClient client = app.CreateClient();

    [Fact]
    public async Task QrTokens_AreLimitedPerCustomer_NotPerAddress()
    {
        var customer = await app.NewCustomerAsync(client);
        var other = await app.NewCustomerAsync(client);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.IssueQrTokenAsync(customer.AccessToken)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.IssueQrTokenAsync(customer.AccessToken)).StatusCode);

        // Другият клиент идва от същия адрес, но има собствен лимит.
        Assert.Equal(HttpStatusCode.OK, (await client.IssueQrTokenAsync(other.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Scans_AreLimitedPerBarista_IncludingInvalidCodes()
    {
        var barista = await app.NewStaffAsync(client);
        var other = await app.NewStaffAsync(client);

        // Изпробването на подправени кодове също харчи от лимита.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.ScanAsync(barista.AccessToken, "подправен код")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.ScanAsync(barista.AccessToken, "подправен код")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.ScanAsync(barista.AccessToken, "подправен код")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.ScanAsync(other.AccessToken, "подправен код")).StatusCode);
    }

    [Fact]
    public async Task Actions_AreLimitedPerBarista()
    {
        var barista = await app.NewStaffAsync(client);
        var other = await app.NewStaffAsync(client);
        var request = ApiClient.Purchase((Guid.NewGuid(), 1));

        // Сесията не съществува, но и отказаните действия харчат от лимита.
        Assert.Equal(HttpStatusCode.NotFound, (await client.SubmitActionAsync(barista, Guid.NewGuid(), request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.ReverseAsync(barista, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SubmitActionAsync(barista, Guid.NewGuid(), request)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.SubmitActionAsync(other, Guid.NewGuid(), request)).StatusCode);
    }
}
