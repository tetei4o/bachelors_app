namespace StoryCups.Shared.Components;

/// <summary>
/// Вариантите на логото, които сайтът и приложението ползват (насоките за марката, ADR-014).
/// Бялото лого е за тъмен фон; знакът е в одобрените двойки: Latte art върху Green bean или Black coffee,
/// Green bean върху Latte art.
/// </summary>
public enum LogoVariant
{
    /// <summary>Вертикалното (основно) лого, бяло.</summary>
    PrimaryWhite,

    /// <summary>Хоризонталното лого, бяло; за места с малко височина (лентата горе).</summary>
    SecondaryWhite,

    /// <summary>Само знакът, Latte art.</summary>
    SymbolLatte,

    /// <summary>Само знакът, Green bean.</summary>
    SymbolGreen,
}
