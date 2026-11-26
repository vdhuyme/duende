using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Skoruba.Duende.IdentityServer.Admin.UI.Api.Dtos.Roles
{
    /// <summary>
    /// Body = identity-data.json content (or just the Roles array).
    /// POST /api/Roles/UpsertClaims
    /// </summary>
    public class RolesClaimsUpsertApiDto
    {
        /// <summary>Optional wrapper to match identity-data.json root.</summary>
        public IdentityDataSectionDto IdentityData { get; set; }

        public List<RoleClaimsUpsertDto> Roles { get; set; } = [];

        public List<RoleClaimsUpsertDto> GetRoles() => IdentityData?.Roles ?? Roles;
    }

    public class IdentityDataSectionDto
    {
        public List<RoleClaimsUpsertDto> Roles { get; set; } = [];
    }

    public class RoleClaimsUpsertDto
    {
        [Required]
        public string Name { get; set; }

        public List<RoleClaimSeedDto> Claims { get; set; } = [];
    }

    public class RoleClaimSeedDto
    {
        [Required]
        public string Type { get; set; }

        [Required]
        public string Value { get; set; }

        public string Description { get; set; }
    }
}
