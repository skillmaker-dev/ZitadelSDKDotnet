namespace ZitadelSDK.Internal;

internal static class TransportSecurity
{
    public static Uri ValidateUri(string value, string settingName, bool allowInsecureTransport)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"{settingName} must be an absolute {(allowInsecureTransport ? "HTTP or HTTPS" : "HTTPS")} URL.");
        }

        EnsureSupportedScheme(uri, settingName, allowInsecureTransport);
        return uri;
    }

    public static string NormalizeAuthority(string authority, string settingName, bool allowInsecureTransport)
    {
        var authorityUri = ValidateUri(authority, settingName, allowInsecureTransport);
        return authorityUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static bool UsesHttps(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureSupportedScheme(Uri uri, string settingName, bool allowInsecureTransport)
    {
        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (allowInsecureTransport && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{settingName} must use {(allowInsecureTransport ? "HTTP or HTTPS" : "HTTPS")}.");
    }
}
