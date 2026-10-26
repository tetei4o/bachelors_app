# Story Cups: система за лоялност

Дипломна работа (ФПМИ, ТУ-София). Карта за лоялност с QR печати за кафене: сайт (Blazor), мобилно приложение (.NET MAUI Blazor Hybrid, Android) и общ REST API върху PostgreSQL.

Версията е само за дипломната работа: без продукционна употреба и без интеграция с кафенето. В разработката и тестовете се ползват само фиктивни данни. Решенията са в [docs/DECISIONS.md](docs/DECISIONS.md).

## Състояние

Срез 1 (skeleton и автентикация):

- Сайт: регистрация, потвърждение на имейл, вход, забравена парола, профил. Страниците за акаунта са статичен SSR, останалите са интерактивни (Blazor Server).
- API `/api/v1`: регистрация, вход, refresh с ротация и отнемане на веригата при повторна употреба, изход, `/me`.
- Мобилно приложение: вход, профил (същият компонент като в сайта), настройка на адреса на сървъра, тестов екран за скенера (spike по ADR-003).
- Тестове: unit и интеграционни срещу истински PostgreSQL (Testcontainers).

Печатите, QR токенът и режимът на баристата идват в следващите срезове.

## Структура

```
src/
  StoryCups.Domain          домейн модел (засега refresh токени и роли)
  StoryCups.Contracts       DTO и пътища на API-то, общи за сървъра и приложението
  StoryCups.Application     интерфейси на use case-ите
  StoryCups.Infrastructure  EF Core + PostgreSQL, Identity, JWT, имейл (MailKit)
  StoryCups.Shared          общи Razor компоненти и стилове (RCL)
  StoryCups.Web             хост: сайт + API
  StoryCups.Mobile          .NET MAUI Blazor Hybrid (Android, Windows)
tests/
  StoryCups.UnitTests
  StoryCups.IntegrationTests
```

## Изисквания

- .NET SDK 10.0.100 или по-нов (виж `global.json`)
- Docker Desktop (PostgreSQL, Mailpit и интеграционните тестове)
- За мобилното приложение: workload `maui` и Android SDK (идват с Visual Studio и „.NET Multi-platform App UI development“)

## Първо пускане на сайта

Командите са за PowerShell от корена на хранилището.

1. База и пощенски сървър за разработка:

   ```powershell
   Copy-Item .env.example .env   # сменете POSTGRES_PASSWORD в .env
   docker compose up -d
   ```

   Mailpit хваща всички писма: http://localhost:8025

2. Тайни в user-secrets (не се качват в git):

   ```powershell
   cd src/StoryCups.Web
   dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=storycups;Username=storycups;Password=<паролата от .env>"
   dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))
   ```

3. Демо акаунти (по избор). Паролите са поне 10 символа:

   ```powershell
   dotnet user-secrets set "Seed:Users:0:Password" "<парола за barista@example.com>"
   dotnet user-secrets set "Seed:Users:1:Password" "<парола за admin@example.com>"
   dotnet user-secrets set "Seed:Users:2:Password" "<парола за client@example.com>"
   ```

   Акаунт без парола се пропуска с предупреждение в лога.

4. Пускане. В Development миграциите се прилагат автоматично:

   ```powershell
   dotnet run --launch-profile https
   ```

   Сайтът е на https://localhost:7060, описанието на API-то (OpenAPI) на https://localhost:7060/openapi/v1.json.

Връзките в писмата се строят от `App:PublicBaseUrl` (по подразбиране `https://localhost:7060/`). Ако пускате профила `http`, задайте го на `http://localhost:5056/`.

## Тестове

```powershell
dotnet test StoryCups.Server.slnf
```

`StoryCups.Server.slnf` съдържа всичко без мобилното приложение, за да минава и без MAUI workload. Интеграционните тестове пускат PostgreSQL в контейнер, затова Docker трябва да работи.

## Мобилно приложение

```powershell
dotnet workload install maui
dotnet build src/StoryCups.Mobile -f net10.0-android
dotnet build src/StoryCups.Mobile -t:Run -f net10.0-android
```

Последната команда инсталира и пуска приложението на отворен емулатор или свързан телефон. От Visual Studio: изберете `StoryCups.Mobile` и Android емулатор.

Адрес на сървъра (екран „Настройки“ в приложението):

- Емулатор: `http://10.0.2.2:5056/` (по подразбиране в Debug). Сайтът трябва да върви с `dotnet run --launch-profile http`.
- Телефон в същата Wi-Fi мрежа: пуснете сайта с `dotnet run --launch-profile http --urls http://0.0.0.0:5056`, разрешете порт 5056 във Windows Firewall и въведете `http://<IP на компютъра>:5056/`.

Нешифрован HTTP е разрешен само в Debug.

### Spike на скенера (ADR-003)

1. Влезте в приложението с акаунт с роля Barista или Admin.
2. Отворете на компютъра https://localhost:7060/dev/qr (само в Development): там има тестов QR код със същата дължина като токена от ADR-005 (99 символа, QR версия 6, ниво M).
3. В приложението: „Скенер“, после „Сканирай“ поне 10 пъти. Екранът показва времето на всеки опит, броя под 2 s и медианата.

Критерият за успех е под 2 s в поне 9 от 10 опита на реален Android телефон.
