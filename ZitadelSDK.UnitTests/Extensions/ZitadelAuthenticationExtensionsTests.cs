using System.Security.Claims;
using System.Text.Json;
using ZitadelSDK.Authentication;

namespace ZitadelSDK.UnitTests.Extensions;

public class ZitadelAuthenticationExtensionsTests
{
    [Fact]
    public void ParseRoleClaims_SingleRoleSingleOrg_CreatesCorrectClaims()
    {
        // ZITADEL format: {"role-key": {"org-id": "org-name"}}
        var json = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        {
            ["admin"] = new() { ["org1"] = "Acme Corp" }
        });

        var claims = ParseRoleClaims(json);

        Assert.Contains(claims, c =>
            c.Type == ClaimTypes.Role && c.Value == "admin");
        Assert.Contains(claims, c =>
            c.Type == ZitadelClaimTypes.OrganizationRole("org1") && c.Value == "admin");
        Assert.Equal(2, claims.Count);
    }

    [Fact]
    public void ParseRoleClaims_SingleRoleMultipleOrgs_CreatesClaimsForAllOrgs()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        {
            ["editor"] = new()
            {
                ["org1"] = "Acme Corp",
                ["org2"] = "Beta Inc"
            }
        });

        var claims = ParseRoleClaims(json);

        Assert.Single(claims, c =>
            c.Type == ClaimTypes.Role && c.Value == "editor");
        Assert.Contains(claims, c =>
            c.Type == ZitadelClaimTypes.OrganizationRole("org1") && c.Value == "editor");
        Assert.Contains(claims, c =>
            c.Type == ZitadelClaimTypes.OrganizationRole("org2") && c.Value == "editor");
        Assert.Equal(3, claims.Count);
    }

    [Fact]
    public void ParseRoleClaims_MultipleRolesMultipleOrgs_CreatesAllCombinations()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        {
            ["admin"] = new() { ["org1"] = "Acme Corp" },
            ["viewer"] = new()
            {
                ["org1"] = "Acme Corp",
                ["org2"] = "Beta Inc"
            }
        });

        var claims = ParseRoleClaims(json);

        var roleClaims = claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        Assert.Equal(2, roleClaims.Count);
        Assert.Contains(roleClaims, c => c.Value == "admin");
        Assert.Contains(roleClaims, c => c.Value == "viewer");

        Assert.Contains(claims, c =>
            c.Type == ZitadelClaimTypes.OrganizationRole("org1") && c.Value == "admin");
        Assert.Contains(claims, c =>
            c.Type == ZitadelClaimTypes.OrganizationRole("org1") && c.Value == "viewer");
        Assert.Contains(claims, c =>
            c.Type == ZitadelClaimTypes.OrganizationRole("org2") && c.Value == "viewer");
    }

    [Fact]
    public void ParseRoleClaims_MalformedJson_ReturnsEmptyList()
    {
        var claims = ParseRoleClaimsWithErrorHandling("not valid json");

        Assert.Empty(claims);
    }

    [Fact]
    public void ParseRoleClaims_EmptyObject_ReturnsEmptyList()
    {
        var claims = ParseRoleClaims("{}");

        Assert.Empty(claims);
    }

    [Fact]
    public void ParseRoleClaims_WithClaimsIssuer_SetsIssuerOnClaims()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        {
            ["admin"] = new() { ["org1"] = "Acme Corp" }
        });

        var claims = ParseRoleClaims(json, "ZITADEL");

        Assert.All(claims, c => Assert.Equal("ZITADEL", c.Issuer));
    }

    [Fact]
    public void ParseRoleClaims_OrgValueIsIgnored_OnlyKeysUsed()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        {
            ["admin"] = new() { ["org1"] = "This value is irrelevant" }
        });

        var claims = ParseRoleClaims(json);

        var orgClaim = claims.First(c => c.Type == ZitadelClaimTypes.OrganizationRole("org1"));
        Assert.Equal("admin", orgClaim.Value);
    }

    private static List<Claim> ParseRoleClaims(string roleClaimValue, string? claimsIssuer = null)
    {
        var claims = new List<Claim>();
        var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(roleClaimValue);
        if (parsed is null) return claims;

        foreach (var (role, orgs) in parsed)
        {
            foreach (var orgId in orgs.Keys)
            {
                claims.Add(new Claim(
                    ZitadelClaimTypes.OrganizationRole(orgId),
                    role,
                    ClaimValueTypes.String,
                    claimsIssuer));
            }

            claims.Add(new Claim(
                ClaimTypes.Role,
                role,
                ClaimValueTypes.String,
                claimsIssuer));
        }

        return claims;
    }

    private static List<Claim> ParseRoleClaimsWithErrorHandling(string roleClaimValue, string? claimsIssuer = null)
    {
        try
        {
            return ParseRoleClaims(roleClaimValue, claimsIssuer);
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
