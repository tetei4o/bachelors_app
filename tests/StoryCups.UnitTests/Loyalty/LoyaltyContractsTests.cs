using System.ComponentModel.DataAnnotations;
using StoryCups.Contracts.Loyalty;

namespace StoryCups.UnitTests.Loyalty;

/// <summary>
/// Проверката на входа на действията (NFR-02): същите правила важат за сървъра и за формата в приложението.
/// </summary>
public sealed class LoyaltyContractsTests
{
    private static readonly Guid Product = Guid.NewGuid();

    [Fact]
    public void Purchase_WithItems_IsValid()
    {
        var request = new LoyaltyActionRequest
        {
            Type = LoyaltyActionTypes.Purchase,
            Items = [new PurchaseItem { ProductId = Product, Quantity = 2 }],
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void RedeemReward_WithProduct_IsValid()
    {
        var request = new LoyaltyActionRequest { Type = LoyaltyActionTypes.RedeemReward, ProductId = Product };

        Assert.Empty(Validate(request));
    }

    public static TheoryData<LoyaltyActionRequest, string> InvalidRequests => new()
    {
        { new LoyaltyActionRequest { Type = "refund", ProductId = Product }, nameof(LoyaltyActionRequest.Type) },
        { new LoyaltyActionRequest { Type = string.Empty }, nameof(LoyaltyActionRequest.Type) },
        { new LoyaltyActionRequest { Type = LoyaltyActionTypes.Reversal }, nameof(LoyaltyActionRequest.Type) },
        { new LoyaltyActionRequest { Type = LoyaltyActionTypes.Purchase }, nameof(LoyaltyActionRequest.Items) },
        { new LoyaltyActionRequest { Type = LoyaltyActionTypes.Purchase, Items = [] }, nameof(LoyaltyActionRequest.Items) },
        { Purchase(new PurchaseItem { ProductId = Product, Quantity = 0 }), nameof(LoyaltyActionRequest.Items) },
        { Purchase(new PurchaseItem { ProductId = Product, Quantity = 21 }), nameof(LoyaltyActionRequest.Items) },
        { Purchase(new PurchaseItem { ProductId = Guid.Empty, Quantity = 1 }), nameof(LoyaltyActionRequest.Items) },
        {
            Purchase(new PurchaseItem { ProductId = Product, Quantity = 15 }, new PurchaseItem { ProductId = Guid.NewGuid(), Quantity = 6 }),
            nameof(LoyaltyActionRequest.Items)
        },
        {
            new LoyaltyActionRequest
            {
                Type = LoyaltyActionTypes.Purchase,
                Items = [new PurchaseItem { ProductId = Product }],
                ProductId = Product,
            },
            nameof(LoyaltyActionRequest.ProductId)
        },
        { new LoyaltyActionRequest { Type = LoyaltyActionTypes.RedeemReward }, nameof(LoyaltyActionRequest.ProductId) },
        { new LoyaltyActionRequest { Type = LoyaltyActionTypes.RedeemReward, ProductId = Guid.Empty }, nameof(LoyaltyActionRequest.ProductId) },
        {
            new LoyaltyActionRequest
            {
                Type = LoyaltyActionTypes.RedeemReward,
                ProductId = Product,
                Items = [new PurchaseItem { ProductId = Product }],
            },
            nameof(LoyaltyActionRequest.Items)
        },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void InvalidRequest_NamesTheField(LoyaltyActionRequest request, string member)
    {
        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("аб", false)]
    [InlineData("абв", true)]
    public void Reversal_RequiresAReason(string reason, bool valid)
    {
        Assert.Equal(valid, Validate(new ReversalRequest { Reason = reason }).Count == 0);
        Assert.NotEmpty(Validate(new ReversalRequest { Reason = new string('а', ReversalRequest.ReasonMaxLength + 1) }));
    }

    [Fact]
    public void Status_CountsStampsToTheNextReward()
    {
        Assert.Equal(3, new LoyaltyStatusDto(5, 0, 8).StampsToNextReward);
        Assert.Equal(8, new LoyaltyStatusDto(0, 2, 8).StampsToNextReward);
    }

    private static LoyaltyActionRequest Purchase(params PurchaseItem[] items) =>
        new() { Type = LoyaltyActionTypes.Purchase, Items = [.. items] };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
