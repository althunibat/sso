// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Identity.Data;
using Identity.Models;
using IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.EntityFramework.Options;

namespace Identity {
    public class SeedData {
        public static async Task EnsureSeedData(string aspnetConnString, string cfgConnString, string opsConnString) {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddSingleton(c => new ConfigurationStoreOptions());
            services.AddSingleton(c => new OperationalStoreOptions());
            services.AddDbContext<ApplicationDbContext>(options =>
               options.UseNpgsql(aspnetConnString));

            services.AddDbContext<ConfigurationDbContext>(options =>
                options.UseNpgsql(cfgConnString));

            services.AddDbContext<PersistedGrantDbContext>(options =>
                options.UseNpgsql(opsConnString));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            await using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
            await context.Database.MigrateAsync();
            var cfgContext = scope.ServiceProvider.GetService<ConfigurationDbContext>();
            await cfgContext.Database.MigrateAsync();
            await scope.ServiceProvider.GetService<PersistedGrantDbContext>().Database.MigrateAsync();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var admin = await roleManager.FindByNameAsync("admin");
            if (admin == null) {
                admin = new IdentityRole("admin");
                var result = await roleManager.CreateAsync(admin);
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
            }

            var user = await roleManager.FindByNameAsync("user");
            if (user == null) {
                user = new IdentityRole("user");
                var result = await roleManager.CreateAsync(user);
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
            }

            var alice = await userMgr.FindByNameAsync("alice");
            if (alice == null) {
                alice = new ApplicationUser {
                    UserName = "alice",
                    Email = "AliceSmith@email.com",
                    EmailConfirmed = true,
                };
                var result = await userMgr.CreateAsync(alice, "Pass123$");
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }

                result = await userMgr.AddClaimsAsync(alice, new Claim[]{
                    new Claim(JwtClaimTypes.Name, "Alice Smith"),
                    new Claim(JwtClaimTypes.GivenName, "Alice"),
                    new Claim(JwtClaimTypes.FamilyName, "Smith"),
                    new Claim(JwtClaimTypes.WebSite, "http://alice.com"),
                });
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                result = await userMgr.AddToRoleAsync(alice, user.Name);
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                Log.Debug("alice created");
            }
            else {
                Log.Debug("alice already exists");
            }

            var bob = await userMgr.FindByNameAsync("bob");
            if (bob == null) {
                bob = new ApplicationUser {
                    UserName = "bob",
                    Email = "BobSmith@email.com",
                    EmailConfirmed = true
                };
                var result = await userMgr.CreateAsync(bob, "Pass123$");
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                result = await userMgr.AddClaimsAsync(bob, new Claim[]{
                    new Claim(JwtClaimTypes.Name, "Bob Smith"),
                    new Claim(JwtClaimTypes.GivenName, "Bob"),
                    new Claim(JwtClaimTypes.FamilyName, "Smith"),
                    new Claim(JwtClaimTypes.WebSite, "http://bob.com"),
                    new Claim("location", "somewhere")
                });
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                result = await userMgr.AddToRoleAsync(bob, user.Name);
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                Log.Debug("bob created");
            }
            else {
                Log.Debug("bob already exists");
            }

            var hamza = await userMgr.FindByNameAsync("hamza");
            if (hamza == null) {
                hamza = new ApplicationUser {
                    UserName = "hamza",
                    Email = "althunibat@outlook.com",
                    EmailConfirmed = true
                };
                var result = await userMgr.CreateAsync(hamza, "Pass123$");
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                result = await userMgr.AddClaimsAsync(hamza, new Claim[]{
                    new Claim(JwtClaimTypes.Name, "Hamza Althunibat"),
                    new Claim(JwtClaimTypes.GivenName, "Hamza"),
                    new Claim(JwtClaimTypes.FamilyName, "Althunibat"),
                    new Claim(JwtClaimTypes.WebSite, "https://hamza.althunibat.info"),
                    new Claim("location", "Dubai")
                });
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                result = await userMgr.AddToRoleAsync(hamza, admin.Name);
                if (!result.Succeeded) {
                    throw new Exception(result.Errors.First().Description);
                }
                Log.Debug("hamza created");
            }
            else {
                Log.Debug("hamza already exists");
            }

            if (!cfgContext.Clients.Any()) {
                Log.Debug("Clients being populated");
                foreach (var client in Config.Clients.ToList()) {
                    cfgContext.Clients.Add(client.ToEntity());
                }
                await cfgContext.SaveChangesAsync();
            }
            else {
                Log.Debug("Clients already populated");
            }

            if (!cfgContext.IdentityResources.Any()) {
                Log.Debug("IdentityResources being populated");
                foreach (var resource in Config.IdentityResources.ToList()) {
                    cfgContext.IdentityResources.Add(resource.ToEntity());
                }
                await cfgContext.SaveChangesAsync();
            }
            else {
                Log.Debug("IdentityResources already populated");
            }

            if (!cfgContext.ApiResources.Any()) {
                Log.Debug("ApiScopes being populated");
                foreach (var resource in Config.ApiScopes.ToList()) {
                    cfgContext.ApiScopes.Add(resource.ToEntity());
                }
                await cfgContext.SaveChangesAsync();
            }
            else {
                Log.Debug("ApiScopes already populated");
            }
        }
    }
}
