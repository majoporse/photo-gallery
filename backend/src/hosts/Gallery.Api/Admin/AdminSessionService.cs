using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gallery.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gallery.Api.Admin;

public sealed class AdminSessionService(IOptions<AdminOptions> admin, IOptions<AuthOptions> auth)
{
    public string? IssueToken(string username, string password)
    {
        var options = auth.Value;

        // Always run both checks, so a wrong username costs the same time as a
        // wrong password and the response reveals neither.
        var usernameMatches = FixedTimeEquals(username, admin.Value.Username);
        var passwordMatches = PasswordHasher.Verify(password, admin.Value.PasswordHash);

        if (!usernameMatches || !passwordMatches)
        {
            return null;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, AdminOptions.Name)],
            notBefore: now,
            expires: now.Add(options.Lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool FixedTimeEquals(string left, string right) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
}
