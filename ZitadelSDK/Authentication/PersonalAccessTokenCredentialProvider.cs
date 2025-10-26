using Grpc.Core;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Provides Personal Access Token (PAT) authentication for ZITADEL.
/// </summary>
public class PersonalAccessTokenCredentialProvider : IZitadelCredentialProvider
{
    private readonly string _token;
    private readonly string _authenticationScheme;

    public PersonalAccessTokenCredentialProvider(string token, string authenticationScheme = "Bearer")
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Personal access token cannot be null or empty.", nameof(token));
        }

        _token = token;
        _authenticationScheme = authenticationScheme;
    }

    public CallCredentials CreateCallCredentials(string authority)
    {
        return CallCredentials.FromInterceptor((context, metadata) =>
        {
            metadata.Add("Authorization", $"{_authenticationScheme} {_token}");
            return Task.CompletedTask;
        });
    }
}
