// Copyright (c) Jan Škoruba. All Rights Reserved.
// Licensed under the Apache License, Version 2.0.

namespace Skoruba.Duende.IdentityServer.Admin.EntityFramework.Extensions.Common
{
    /// <summary>
    /// Optional description on a claim entity so admins can see what the claim is for.
    /// Implemented by concrete claim entities (e.g. UserIdentityRoleClaim); the generic
    /// repositories detect it at runtime without tightening generic constraints.
    /// </summary>
    public interface IDescribableClaim
    {
        string Description { get; set; }
    }
}
