using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using StoryCups.Contracts.Profile;
using StoryCups.Domain.Identity;
using StoryCups.Infrastructure.Identity;

namespace StoryCups.IntegrationTests.Infrastructure;

/// <summary>Влязъл тестов потребител: Id, имейл и access токен.</summary>
public sealed record TestUser(Guid Id, string Email, string AccessToken);

/// <summary>
/// Фиктивни акаунти за тестовете. Клиентът минава по целия път през API-то (регистрация, писмо, вход);
/// персоналът се създава директно, защото няма саморегистрация за служители (ADR-004).
/// </summary>
public static class TestAccounts
{
    public static async Task<TestUser> NewCustomerAsync(this StoryCupsAppFactory app, HttpClient client)
    {
        var email = ApiClient.NewEmail();
        await client.RegisterAndConfirmAsync(app.Emails, email);
        return await client.SignInAsync(email);
    }

    public static async Task<TestUser> NewStaffAsync(this StoryCupsAppFactory app, HttpClient client, string role = AppRoles.Barista)
    {
        var email = ApiClient.NewEmail();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Тест Служител",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var created = await users.CreateAsync(user, ApiClient.Password);
            Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(e => e.Description)));
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }

        return await client.SignInAsync(email);
    }

    private static async Task<TestUser> SignInAsync(this HttpClient client, string email)
    {
        var tokens = await client.LoginOkAsync(email);
        var me = await client.GetMeAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<ProfileDto>();
        return new TestUser(profile!.Id, email, tokens.AccessToken);
    }
}
