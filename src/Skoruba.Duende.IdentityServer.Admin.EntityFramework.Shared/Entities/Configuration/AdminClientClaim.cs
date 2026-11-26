// Copyright (c) Jan Škoruba. All Rights Reserved.
// Licensed under the Apache License, Version 2.0.

using Duende.IdentityServer.EntityFramework.Entities;

namespace Skoruba.Duende.IdentityServer.Admin.EntityFramework.Shared.Entities.Configuration
{
    /// <summary>
    /// Extends the Duende <see cref="ClientClaim"/> with an optional description
    /// so admins can see what each client claim is for. Mapped via TPH onto the
    /// existing ClientClaims table.
    /// </summary>
    public class AdminClientClaim : ClientClaim
    {
        /// <summary>
        /// Optional human-readable description explaining what this claim is for.
        /// </summary>
        public string Description { get; set; }
    }
}
