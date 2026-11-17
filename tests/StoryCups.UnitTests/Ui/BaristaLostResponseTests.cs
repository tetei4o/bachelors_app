using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StoryCups.Contracts.Common;
using StoryCups.Contracts.Loyalty;
using StoryCups.Shared.Pages;
using StoryCups.Shared.Services;

namespace StoryCups.UnitTests.Ui;

/// <summary>
/// Екранът на баристата, когато отговорът на сървъра се изгуби (ADR-007). Компонентът се изпълнява с bUnit
/// срещу фалшива услуга: тестовете следят какво вижда баристата и какво се изпраща към сървъра.
/// </summary>
public sealed class BaristaLostResponseTests : BunitContext
{
    private static readonly ProductDto Cappuccino = new(Guid.NewGuid(), "Cappuccino", 180, 2.60m, EarnsStamp: true);

    private readonly FakeTimeProvider time = new();
    private readonly FakeBaristaService service;

    public BaristaLostResponseTests()
    {
        service = new FakeBaristaService(time);
        Services.AddSingleton<IBaristaService>(service);
        Services.AddSingleton<IQrScanner>(new FakeScanner());
        Services.AddSingleton<TimeProvider>(time);
    }

    [Fact]
    public void LostResponse_SaysThereIsNoConfirmation_AndBlocksOtherActions()
    {
        service.ActionResults.Enqueue(NetworkError());
        var screen = ScanAndPickOneDrink();

        Button(screen, "Начисли печати").Click();

        // Отговорът е изгубен, но действието може да е записано: екранът не твърди обратното.
        Assert.Contains("Няма потвърждение от сървъра.", screen.Markup);
        Assert.Contains("Не е ясно дали действието е записано.", screen.Markup);
        Assert.DoesNotContain("НЕ е записано", screen.Markup);

        // Докато изходът е неясен, второ начисляване и „Готово“ не са възможни.
        Assert.True(Button(screen, "Начисли печати").HasAttribute("disabled"));
        Assert.True(Button(screen, "Готово, следващ клиент").HasAttribute("disabled"));
    }

    [Fact]
    public void Retry_SendsTheSameIdempotencyKey_AndShowsTheServerResult()
    {
        service.ActionResults.Enqueue(NetworkError());
        service.ActionResults.Enqueue(Recorded(stampsAdded: 1, new LoyaltyStatusDto(4, 0, 8)));
        var screen = ScanAndPickOneDrink();
        Button(screen, "Начисли печати").Click();

        Button(screen, "Опитай отново").Click();

        Assert.Equal(2, service.Actions.Count);
        Assert.Equal(service.Actions[0].IdempotencyKey, service.Actions[1].IdempotencyKey);
        Assert.Equal(service.Actions[0].ScanSessionId, service.Actions[1].ScanSessionId);
        Assert.DoesNotContain("Няма потвърждение от сървъра.", screen.Markup);
        Assert.Contains("+1 печат.", screen.Markup);
        Assert.Contains("Баланс: 4 от 8 печата", screen.Markup);
    }

    [Fact]
    public void Cancel_ClosesTheSession_AndTheNewScanShowsTheServerBalance()
    {
        service.ActionResults.Enqueue(NetworkError());
        var screen = ScanAndPickOneDrink();
        Button(screen, "Начисли печати").Click();

        // Сървърът е записал действието, но отговорът не е стигнал до екрана.
        service.Balance = new LoyaltyStatusDto(4, 0, 8);
        Button(screen, "Откажи").Click();

        // Няма екран за избор със стария баланс, от който покупката да се начисли втори път с нов ключ.
        Assert.Empty(Buttons(screen, "Начисли печати"));
        Assert.DoesNotContain("3 от 8 печата", screen.Markup);
        Assert.Contains("Не е ясно дали действието е записано. Сканирайте кода на клиента отново", screen.Markup);
        Assert.Single(service.Actions);

        Button(screen, "Сканирай код").Click();

        Assert.Equal(2, service.Scans);
        Assert.Contains("4 от 8 печата", screen.Markup);
    }

    [Fact]
    public void CancelledReversal_KeepsTheActionOnScreen_SoTheUndoCanBeAskedAgain()
    {
        service.ActionResults.Enqueue(Recorded(stampsAdded: 1, new LoyaltyStatusDto(4, 0, 8)));
        service.ReversalResults.Enqueue(NetworkError());
        var screen = ScanAndPickOneDrink();
        Button(screen, "Начисли печати").Click();
        Button(screen, "Отмени това действие").Click();
        screen.Find("#undo-reason").Change("грешен брой");
        screen.Find("form.sc-undo").Submit();
        Assert.Contains("Няма потвърждение от сървъра.", screen.Markup);

        Button(screen, "Откажи").Click();

        Assert.Contains("Не е ясно дали отмяната е записана.", screen.Markup);
        Assert.Contains("+1 печат.", screen.Markup);
        Assert.DoesNotContain("Баланс:", screen.Markup);
        Assert.Single(Buttons(screen, "Отмени това действие"));
        Assert.Single(service.Reversals);
    }

    private IRenderedComponent<Barista> ScanAndPickOneDrink()
    {
        var screen = Render<Barista>();
        Button(screen, "Сканирай код").Click();
        Assert.Contains("3 от 8 печата", screen.Markup);
        screen.Find($"button[aria-label='Добави {Cappuccino.Name}']").Click();
        return screen;
    }

    private static IElement Button(IRenderedComponent<Barista> screen, string text) => Assert.Single(Buttons(screen, text));

    private static List<IElement> Buttons(IRenderedComponent<Barista> screen, string text) =>
        [.. screen.FindAll("button").Where(button => button.TextContent.Contains(text, StringComparison.Ordinal))];

    private static ClientResult<LoyaltyActionResultDto> NetworkError() =>
        ClientResult<LoyaltyActionResultDto>.Failure(ClientErrorCodes.Network, "Няма връзка със сървъра.");

    private ClientResult<LoyaltyActionResultDto> Recorded(int stampsAdded, LoyaltyStatusDto after)
    {
        var now = time.GetUtcNow();
        return ClientResult<LoyaltyActionResultDto>.Success(new LoyaltyActionResultDto(
            Guid.NewGuid(), LoyaltyActionTypes.Purchase, now, stampsAdded, 0, 0, after, now.AddMinutes(5), null));
    }

    private sealed class FakeScanner : IQrScanner
    {
        public bool HasCamera => true;

        public Task<QrScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new QrScanResult(QrScanStatus.Scanned, "код от картата на клиента"));
    }

    /// <summary>Записва какво изпраща екранът и връща предварително зададени отговори.</summary>
    private sealed class FakeBaristaService(TimeProvider time) : IBaristaService
    {
        public LoyaltyStatusDto Balance { get; set; } = new(3, 0, 8);

        public int Scans { get; private set; }

        public List<(Guid ScanSessionId, Guid IdempotencyKey)> Actions { get; } = [];

        public List<(Guid ActionId, Guid IdempotencyKey)> Reversals { get; } = [];

        public Queue<ClientResult<LoyaltyActionResultDto>> ActionResults { get; } = new();

        public Queue<ClientResult<LoyaltyActionResultDto>> ReversalResults { get; } = new();

        public Task<ClientResult<IReadOnlyList<ProductDto>>> GetProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ClientResult<IReadOnlyList<ProductDto>>.Success([Cappuccino]));

        public Task<ClientResult<ScanSessionDto>> ScanAsync(string token, CancellationToken cancellationToken = default)
        {
            Scans++;
            return Task.FromResult(ClientResult<ScanSessionDto>.Success(
                new ScanSessionDto(Guid.NewGuid(), time.GetUtcNow().AddMinutes(2), 120, "ИП", Balance)));
        }

        public Task<ClientResult<LoyaltyActionResultDto>> SubmitActionAsync(
            Guid scanSessionId, LoyaltyActionRequest request, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            Actions.Add((scanSessionId, idempotencyKey));
            return Task.FromResult(ActionResults.Dequeue());
        }

        public Task<ClientResult<LoyaltyActionResultDto>> ReverseAsync(
            Guid actionId, string reason, Guid idempotencyKey, CancellationToken cancellationToken = default)
        {
            Reversals.Add((actionId, idempotencyKey));
            return Task.FromResult(ReversalResults.Dequeue());
        }
    }
}
