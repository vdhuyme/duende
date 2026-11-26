// Copyright (c) Jan Škoruba. All Rights Reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared.DbContexts;
using Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared.Entities.Identity;

namespace Skoruba.Duende.IdentityServer.STS.Identity.Helpers
{
    public class ClaimsProfileService(
        UserManager<UserIdentity> userManager,
        RoleManager<UserIdentityRole> roleManager,
        AdminIdentityDbContext dbContext) : IProfileService
    {
        private readonly UserManager<UserIdentity> _userManager = userManager;
        private readonly RoleManager<UserIdentityRole> _roleManager = roleManager;
        private readonly AdminIdentityDbContext _dbContext = dbContext;

        public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken cancellationToken = default)
        {
            var user = await FindUserAsync(context);
            if (user is null)
            {
                return;
            }

            // Load entities directly so Description is available (GetClaimsAsync only returns Type/Value).
            var userClaimEntities = await _dbContext.Set<UserIdentityUserClaim>()
                .Where(c => c.UserId == user.Id)
                .ToListAsync(cancellationToken);

            var userClaims = userClaimEntities
                .Select(c => new ClaimWithDescription(c.ClaimType, c.ClaimValue, c.Description))
                .ToList();

            AddClaimIfMissing(userClaims, JwtClaimTypes.Subject, user.Id);
            AddClaimIfMissing(userClaims, JwtClaimTypes.PreferredUserName, user.UserName);
            AddClaimIfMissing(userClaims, JwtClaimTypes.Name, user.UserName);
            AddClaimIfMissing(userClaims, JwtClaimTypes.Email, user.Email);
            AddClaimIfMissing(userClaims, JwtClaimTypes.EmailVerified, user.EmailConfirmed ? "true" : "false");

            var roles = await _userManager.GetRolesAsync(user);
            var roleClaims = new List<ClaimWithDescription>();

            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role is null)
                {
                    continue;
                }

                var roleClaimEntities = await _dbContext.Set<UserIdentityRoleClaim>()
                    .Where(c => c.RoleId == role.Id)
                    .ToListAsync(cancellationToken);

                roleClaims.AddRange(roleClaimEntities
                    .Select(c => new ClaimWithDescription(c.ClaimType, c.ClaimValue, c.Description)));
            }

            var mergedClaims = userClaims
                .Where(c => !IsStandardClaim(c.Type))
                .Concat(roleClaims)
                .GroupBy(c => new { c.Type, c.Value })
                .Select(g => g.First())
                .ToList();

            var issuedClaims = userClaims
                .Where(c => IsStandardClaim(c.Type))
                .Select(c => new Claim(c.Type, c.Value))
                .Concat(roles.Select(role => new Claim(JwtClaimTypes.Role, role)))
                .ToList();

            // Only expose the aggregated claims (with Description) through the UserInfo API
            // (/connect/userinfo), never inside the id_token / access_token.
            if (context.Caller == IdentityServerConstants.ProfileDataCallers.UserInfoEndpoint)
            {
                issuedClaims.Add(CreateJsonClaim("claims", mergedClaims));
            }

            context.IssuedClaims.AddRange(issuedClaims
                .GroupBy(claim => new { claim.Type, claim.Value })
                .Select(group => group.First()));
        }

        private static Claim CreateJsonClaim(string type, IEnumerable<ClaimWithDescription> claims)
        {
            var values = claims
                .Select(c => new { claimType = c.Type, claimValue = c.Value, description = c.Description })
                .ToArray();

            return new Claim(type, JsonSerializer.Serialize(values), JsonClaimValueTypes.JsonArray);
        }

        private static bool IsStandardClaim(string type)
        {
            return type == JwtClaimTypes.Subject ||
                   type == JwtClaimTypes.PreferredUserName ||
                   type == JwtClaimTypes.Name ||
                   type == JwtClaimTypes.Email ||
                   type == JwtClaimTypes.EmailVerified ||
                   type == JwtClaimTypes.Role;
        }

        private static void AddClaimIfMissing(ICollection<ClaimWithDescription> claims, string type, string value)
        {
            if (!string.IsNullOrEmpty(value) && !claims.Any(c => c.Type == type))
            {
                claims.Add(new ClaimWithDescription(type, value, null));
            }
        }

        public async Task IsActiveAsync(IsActiveContext context, CancellationToken cancellationToken = default)
        {
            var user = await FindUserAsync(context);
            context.IsActive = user != null && !await _userManager.IsLockedOutAsync(user);
        }

        private async Task<UserIdentity> FindUserAsync(ProfileDataRequestContext context)
        {
            var subjectId = context.Subject?.FindFirst(JwtClaimTypes.Subject)?.Value;
            return subjectId == null ? null : await _userManager.FindByIdAsync(subjectId);
        }

        private async Task<UserIdentity> FindUserAsync(IsActiveContext context)
        {
            var subjectId = context.Subject?.FindFirst(JwtClaimTypes.Subject)?.Value;
            return subjectId == null ? null : await _userManager.FindByIdAsync(subjectId);
        }

        private sealed record ClaimWithDescription(string Type, string Value, string Description);
    }
}
