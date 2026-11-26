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
using Microsoft.IdentityModel.JsonWebTokens;
using Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared.Entities.Identity;

namespace Skoruba.Duende.IdentityServer.STS.Identity.Helpers
{
    public class ClaimsProfileService(
        UserManager<UserIdentity> userManager,
        RoleManager<UserIdentityRole> roleManager) : IProfileService
    {
        private readonly UserManager<UserIdentity> _userManager = userManager;
        private readonly RoleManager<UserIdentityRole> _roleManager = roleManager;

        public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken cancellationToken = default)
        {
            var user = await FindUserAsync(context);
            if (user is null)
            {
                return;
            }

            var userClaims = (await _userManager.GetClaimsAsync(user)).ToList();
            AddClaimIfMissing(userClaims, JwtClaimTypes.Subject, user.Id);
            AddClaimIfMissing(userClaims, JwtClaimTypes.PreferredUserName, user.UserName);
            AddClaimIfMissing(userClaims, JwtClaimTypes.Name, user.UserName);
            AddClaimIfMissing(userClaims, JwtClaimTypes.Email, user.Email);
            AddClaimIfMissing(userClaims, JwtClaimTypes.EmailVerified, user.EmailConfirmed ? "true" : "false");

            var roles = await _userManager.GetRolesAsync(user);
            var roleClaims = new List<Claim>();

            foreach (var roleName in roles)
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role is not null)
                {
                    roleClaims.AddRange(await _roleManager.GetClaimsAsync(role));
                }
            }

            var mergedClaims = userClaims
                .Where(claim => !IsStandardClaim(claim.Type))
                .Concat(roleClaims)
                .GroupBy(claim => new { claim.Type, claim.Value })
                .Select(group => group.First());

            var issuedClaims = userClaims.Where(claim => IsStandardClaim(claim.Type))
                .Concat(roles.Select(role => new Claim(JwtClaimTypes.Role, role)))
                .ToList();

            // Only expose the aggregated claims through the UserInfo API (/connect/userinfo),
            // never inside the id_token / access_token.
            if (context.Caller == IdentityServerConstants.ProfileDataCallers.UserInfoEndpoint)
            {
                issuedClaims.Add(CreateJsonClaim("claims", mergedClaims));
            }
            context.IssuedClaims.AddRange(issuedClaims
                .GroupBy(claim => new { claim.Type, claim.Value })
                .Select(group => group.First()));
        }

        private static Claim CreateJsonClaim(string type, IEnumerable<Claim> claims)
        {
            var values = claims
                .Select(claim => new { claimType = claim.Type, claimValue = claim.Value })
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

        private static void AddClaimIfMissing(ICollection<Claim> claims, string type, string value)
        {
            if (!string.IsNullOrEmpty(value) && !claims.Any(claim => claim.Type == type))
            {
                claims.Add(new Claim(type, value));
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
    }
}
