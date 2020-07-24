using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Identity.Models;
using IdentityModel;
using IdentityServer4;
using Microsoft.AspNetCore.Identity;

namespace Identity {
    public static class Extensions {
        public static async Task<IEnumerable<Claim>> GetClaims(this ApplicationUser user, UserManager<ApplicationUser> manager) {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var claims = await manager.GetClaimsAsync(user);

            claims.Add(new Claim("https://hasura.io/jwt/claims", JsonSerializer.Serialize(new HasuraClaim {
                UserId = user.Id,
                DefaultRole = (await manager.GetRolesAsync(user)).FirstOrDefault(),
                Roles = (await manager.GetRolesAsync(user)).ToArray()
            }), IdentityServerConstants.ClaimValueTypes.Json));
            claims.Add(new Claim(JwtClaimTypes.Subject,user.Id));

            return claims;
        }

        public class HasuraClaim {
            [JsonPropertyName("x-hasura-user-id")]
            public string UserId { get; set; }

            [JsonPropertyName("x-hasura-default-role")]
            public string DefaultRole { get; set; }

            [JsonPropertyName("x-hasura-allowed-roles")]
            public string[] Roles { get; set; }
        }
    }

}