using System.Security.Claims;

namespace ForikAuction.Services;

public static class ClaimsExtensions
{
    /// <summary>Внутренний Id пользователя (claim app_uid), добавляемый при входе.</summary>
    public static int? AppUserId(this ClaimsPrincipal? user)
    {
        var v = user?.FindFirst("app_uid")?.Value;
        return int.TryParse(v, out var id) ? id : null;
    }
}
