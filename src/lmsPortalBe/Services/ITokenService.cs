using lmsPortalBe.Models;

namespace lmsPortalBe.Services
{
    public interface ITokenService
    {
        string CreateAccessToken(ApplicationUser user, IList<string> roles);

        Task<AuthTokens> CreateTokensAsync(ApplicationUser user, IList<string> roles);

        Task<AuthTokens> RefreshTokenAsync(string refreshToken);

        Task RevokeRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Revokes every active refresh token for a user, signing them out of all
        /// devices. Used after a password reset so stolen sessions cannot outlive it.
        /// </summary>
        Task RevokeAllRefreshTokensForUserAsync(string userId);
    }
}
