using Grpc.Core;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Provides Personal Access Token (PAT) authentication for ZITADEL.
/// </summary>
public class PersonalAccessTokenCredentialProvider : IZitadelCredentialProvider
{
    private readonly string _token;
    private readonly string _authenticationScheme;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalAccessTokenCredentialProvider"/> class.
    /// </summary>
    /// <param name="token">The personal access token to use for authentication.</param>
    /// <param name="authenticationScheme">The authentication scheme (default: "Bearer").</param>
    public PersonalAccessTokenCredentialProvider(string token, string authenticationScheme = "Bearer")
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Personal access token cannot be null or empty.", nameof(token));
        }

        _token = token;
        _authenticationScheme = authenticationScheme;
    }

    /// <summary>
    /// Creates call credentials for gRPC authentication using the personal access token.
    /// </summary>
    /// <param name="authority">The authority for the call.</param>
    /// <returns>The call credentials.</returns>
    public CallCredentials CreateCallCredentials(string authority)
    {
        return CallCredentials.FromInterceptor((context, metadata) =>
        {
            metadata.Add("Authorization", $"{_authenticationScheme} {_token}");
            return Task.CompletedTask;
        });
    }
}
