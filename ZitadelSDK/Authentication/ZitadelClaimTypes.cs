namespace ZitadelSDK.Authentication;

/// <summary>
/// Provides ZITADEL-specific claim type constants.
/// </summary>
public static class ZitadelClaimTypes
{
    /// <summary>
    /// The claim type for organization roles.
    /// </summary>
    public const string OrganizationRolePrefix = "urn:zitadel:iam:org:project:role:";

    /// <summary>
    /// The claim type for roles.
    /// </summary>
    public const string Role = "urn:zitadel:iam:org:project:roles";

    /// <summary>
    /// The claim type for project roles.
    /// </summary>
    public const string ProjectRoles = "urn:zitadel:iam:org:project:roles";

    /// <summary>
    /// The claim type for user ID.
    /// </summary>
    public const string UserId = "urn:zitadel:iam:user:id";

    /// <summary>
    /// The claim type for organization ID.
    /// </summary>
    public const string OrganizationId = "urn:zitadel:iam:org:id";

    /// <summary>
    /// The claim type for project ID.
    /// </summary>
    public const string ProjectId = "urn:zitadel:iam:org:project:id";

    /// <summary>
    /// Creates an organization-specific role claim type.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <returns>The formatted claim type.</returns>
    public static string OrganizationRole(string organizationId)
    {
        return $"{OrganizationRolePrefix}{organizationId}";
    }
}
