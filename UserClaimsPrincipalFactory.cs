using System.Security.Claims;
using System.Threading.Tasks;
using Identity.Models;
using IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Identity {
    public class UserClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser> {
        private readonly UserManager<ApplicationUser> _manager;
        public UserClaimsPrincipalFactory(UserManager<ApplicationUser> manager) {
            _manager = manager;
        }
        public async  Task<ClaimsPrincipal> CreateAsync(ApplicationUser user) {
            var identity = new ClaimsIdentity("godwit-sso", JwtClaimTypes.Name,
                JwtClaimTypes.Role);
            identity.AddClaims( await user.GetClaims(_manager));
            return new ClaimsPrincipal(identity);
        }
    }
}