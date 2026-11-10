using StoryCups.Contracts.Common;
using StoryCups.Contracts.Loyalty;
using StoryCups.Infrastructure.Seeding;
using StoryCups.IntegrationTests.Infrastructure;
using StoryCups.RestClient;

namespace StoryCups.IntegrationTests;

/// <summary>
/// Клиентът на API-то, който ползва мобилното приложение (StoryCups.RestClient), срещу сървъра в паметта.
/// Така кодът зад екраните в приложението се проверява без емулатор: MAUI проектът добавя само адреса,
/// токена и камерата.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class RestClientTests(StoryCupsAppFactory app)
{
    private static readonly Guid Cappuccino = MenuSeed.Drinks[2].Id;

    private readonly HttpClient http = app.CreateClient();

    [Fact]
    public async Task Client_RunsTheWholeVisitThroughTheApi()
    {
        var customer = await app.NewCustomerAsync(http);
        var barista = await app.NewStaffAsync(http);
        var card = ClientFor(customer);
        var counter = ClientFor(barista);

        var token = await card.IssueQrTokenAsync();
        var session = await counter.ScanAsync(token.Value!.Token);
        var products = await counter.GetProductsAsync();
        var purchase = await counter.SubmitActionAsync(session.Value!.Id, ApiClient.Purchase((Cappuccino, 3)), Guid.NewGuid());

        Assert.True(purchase.IsSuccess, purchase.ErrorMessage);
        Assert.Equal(12, products.Value!.Count);
        Assert.Equal("ТК", session.Value.CustomerInitials);
        Assert.Equal(new LoyaltyStatusDto(3, 0, 8), purchase.Value!.Loyalty);
        Assert.Equal(new LoyaltyStatusDto(3, 0, 8), (await card.GetStatusAsync()).Value);

        var reversal = await counter.ReverseAsync(purchase.Value.ActionId, "грешен брой", Guid.NewGuid());
        var history = await card.GetHistoryAsync();
        var older = await card.GetHistoryAsync(before: 2);

        Assert.Equal(purchase.Value.ActionId, reversal.Value!.ReversedActionId);
        Assert.Equal([2, 1], history.Value!.Items.Select(item => item.Sequence));
        Assert.Equal(1, Assert.Single(older.Value!.Items).Sequence);
    }

    /// <summary>
    /// ADR-007: заявката стига до сървъра, но отговорът се губи. Приложението вижда грешка в мрежата,
    /// повтаря със същия ключ и получава резултата на вече записаното действие; печатите са начислени веднъж.
    /// </summary>
    [Fact]
    public async Task Client_LostResponse_RetryWithTheSameKey_IsAppliedOnce()
    {
        var customer = await app.NewCustomerAsync(http);
        var barista = await app.NewStaffAsync(http);
        var session = (await ClientFor(barista).ScanAsync((await ClientFor(customer).IssueQrTokenAsync()).Value!.Token)).Value!;
        var unreliable = ClientFor(barista, app.CreateDefaultClient(new LoseFirstResponseHandler()));
        var request = ApiClient.Purchase((Cappuccino, 2));
        var key = Guid.NewGuid();

        var first = await unreliable.SubmitActionAsync(session.Id, request, key);
        var retry = await unreliable.SubmitActionAsync(session.Id, request, key);

        Assert.True(first.IsNetworkError);
        Assert.True(retry.IsSuccess, retry.ErrorMessage);
        Assert.Equal(2, retry.Value!.StampsAdded);
        Assert.Equal(new LoyaltyStatusDto(2, 0, 8), (await ClientFor(customer).GetStatusAsync()).Value);
        Assert.Single((await ClientFor(customer).GetHistoryAsync()).Value!.Items);
    }

    [Fact]
    public async Task Client_ServerUnreachableOrSlow_ReportsNetworkErrorInsteadOfThrowing()
    {
        var barista = await app.NewStaffAsync(http);
        var offline = ClientFor(barista, new HttpClient(new FailingHandler(new HttpRequestException("няма мрежа"))));
        var slow = ClientFor(barista, new HttpClient(new FailingHandler(new TaskCanceledException("изтекло време"))));

        var scan = await offline.ScanAsync("код");
        var action = await slow.SubmitActionAsync(Guid.NewGuid(), ApiClient.Purchase((Cappuccino, 1)), Guid.NewGuid());

        Assert.True(scan.IsNetworkError);
        Assert.Equal(ClientErrorCodes.Network, action.ErrorCode);
        Assert.Equal("Няма връзка със сървъра.", action.ErrorMessage);
    }

    [Fact]
    public async Task Client_CancelledByTheScreen_StopsWithoutAnErrorResult()
    {
        var barista = await app.NewStaffAsync(http);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ClientFor(barista).GetProductsAsync(cancelled.Token));
    }

    [Fact]
    public async Task Client_MapsProblemDetailsToCodeAndMessage()
    {
        var customer = await app.NewCustomerAsync(http);
        var barista = await app.NewStaffAsync(http);
        var counter = ClientFor(barista);

        var invalidCode = await counter.ScanAsync("това не е код");
        var emptyCode = await counter.ScanAsync(string.Empty);
        var noSession = await counter.SubmitActionAsync(Guid.NewGuid(), ApiClient.Purchase((Cappuccino, 1)), Guid.NewGuid());
        var wrongRole = await ClientFor(customer).ScanAsync("код");

        Assert.Equal((LoyaltyErrorCodes.QrInvalid, "Невалиден код."), (invalidCode.ErrorCode, invalidCode.ErrorMessage));
        Assert.Equal("Липсва съдържанието на QR кода.", emptyCode.ErrorMessage);
        Assert.Equal(LoyaltyErrorCodes.ScanSessionNotFound, noSession.ErrorCode);
        Assert.Equal(ClientErrorCodes.Forbidden, wrongRole.ErrorCode);
        Assert.False(invalidCode.IsNetworkError);
    }

    [Fact]
    public async Task Client_WithoutSessionOrWithRejectedToken_ReportsUnauthorized()
    {
        var signedOut = new TestConnection(http.BaseAddress!, accessToken: null);
        var rejected = new TestConnection(http.BaseAddress!, accessToken: "не.е.токен");

        var withoutToken = await new LoyaltyApiClient(new ApiCaller(http, signedOut)).GetStatusAsync();
        var withBadToken = await new LoyaltyApiClient(new ApiCaller(http, rejected)).GetStatusAsync();

        Assert.Equal(ClientErrorCodes.Unauthorized, withoutToken.ErrorCode);
        Assert.Equal(0, signedOut.UnauthorizedCount);
        Assert.Equal(ClientErrorCodes.Unauthorized, withBadToken.ErrorCode);

        // Сървърът е отказал токена: приложението научава, за да излезе локално и да покаже входа.
        Assert.Equal(1, rejected.UnauthorizedCount);
    }

    [Fact]
    public async Task Client_WithoutServerAddress_ExplainsWhatToDo()
    {
        var notConfigured = new TestConnection(baseAddress: null, accessToken: "токен");

        var result = await new LoyaltyApiClient(new ApiCaller(http, notConfigured)).GetStatusAsync();

        Assert.Equal(ClientErrorCodes.Network, result.ErrorCode);
        Assert.Equal("Задайте адреса на сървъра в настройките.", result.ErrorMessage);
    }

    private LoyaltyApiClient ClientFor(TestUser user, HttpClient? httpClient = null) =>
        new(new ApiCaller(httpClient ?? http, new TestConnection(http.BaseAddress!, user.AccessToken)));

    private sealed class TestConnection(Uri? baseAddress, string? accessToken) : IApiConnection
    {
        public int UnauthorizedCount { get; private set; }

        public Uri Resolve(string path) =>
            new(baseAddress ?? throw new InvalidOperationException("Адресът на сървъра не е зададен."), path.TrimStart('/'));

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(accessToken);

        public Task OnUnauthorizedAsync()
        {
            UnauthorizedCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Пропуска заявката до сървъра, но първия отговор го „губи“ по пътя обратно.</summary>
    private sealed class LoseFirstResponseHandler : DelegatingHandler
    {
        private bool lost;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!lost && request.Method == HttpMethod.Post)
            {
                lost = true;
                response.Dispose();
                throw new HttpRequestException("Връзката прекъсна, преди да дойде отговорът.");
            }

            return response;
        }
    }

    private sealed class FailingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
