using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace ZitadelSDK.Authentication;

internal static class JwtSigningCredentialsFactory
{
    private static readonly CryptoProviderFactory NonCachingCryptoProviderFactory = new()
    {
        CacheSignatureProviders = false
    };

    public static SigningCredentials CreateFromPem(string pemKey, string keyId)
    {
        using var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(pemKey);
        var rsaParameters = rsaKey.ExportParameters(true);

        var securityKey = new RsaSecurityKey(rsaParameters)
        {
            KeyId = keyId,
            CryptoProviderFactory = NonCachingCryptoProviderFactory
        };

        return new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
    }
}