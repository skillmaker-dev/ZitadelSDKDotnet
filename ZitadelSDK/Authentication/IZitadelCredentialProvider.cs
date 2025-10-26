using Grpc.Core;

namespace ZitadelSDK.Authentication;

/// <summary>
/// Provides credentials for authenticating with ZITADEL.
/// </summary>
public interface IZitadelCredentialProvider
{
    /// <summary>
    /// Creates gRPC call credentials for the ZITADEL SDK.
    /// </summary>
    /// <param name="authority">The ZITADEL authority URL.</param>
    /// <returns>The configured call credentials.</returns>
    CallCredentials CreateCallCredentials(string authority);
}
