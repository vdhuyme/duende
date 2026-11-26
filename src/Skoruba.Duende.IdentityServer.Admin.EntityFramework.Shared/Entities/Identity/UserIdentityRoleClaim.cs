// Copyright (c) Jan Škoruba. All Rights Reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.AspNetCore.Identity;
using Skoruba.Duende.IdentityServer.Admin.EntityFramework.Extensions.Common;

namespace Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared.Entities.Identity
{
    public class UserIdentityRoleClaim : IdentityRoleClaim<string>, IDescribableClaim
    {
        /// <summary>
        /// Optional human-readable description explaining what this claim is for.
        /// </summary>
        public string Description { get; set; }
    }
}