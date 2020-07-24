// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using IdentityServer4;
using Identity.Data;
using Identity.Models;
using IdentityServer4.EntityFramework.Stores;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using StackExchange.Redis;

namespace Identity {
    public class Startup {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration) {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services) {
            services.AddControllersWithViews()
                .AddJsonOptions(opt => {
                    opt.JsonSerializerOptions.IgnoreNullValues = false;
                    opt.JsonSerializerOptions.WriteIndented = false;
                });

            var path = Path.Combine(Configuration["CERT_PATH"] ?? "", Configuration["CERT_FILENAME"]);
            var certificate =
                new X509Certificate2(path, Configuration["CERT_PASSWORD"]);

            var aspConnString = $"Server={Configuration["DB_HOST"] ?? "localhost"};Port={Configuration["DB_PORT"] ?? "5432"};Database={Configuration["ASPNET_DB"] ?? "aspnet_db"};User Id={Configuration["DB_USER"] ?? "postgres"};Password={Configuration["DB_PASS"]};";
            var cfgConnString = $"Server={Configuration["DB_HOST"] ?? "localhost"};Port={Configuration["DB_PORT"] ?? "5432"};Database={Configuration["IDCFG_DB"] ?? "idcfg_db"};User Id={Configuration["DB_USER"] ?? "postgres"};Password={Configuration["DB_PASS"] };";
            var opsConnString = $"Server={Configuration["DB_HOST"] ?? "localhost"};Port={Configuration["DB_PORT"] ?? "5432"};Database={Configuration["IDOPS_DB"] ?? "idops_db"};User Id={Configuration["DB_USER"] ?? "postgres"};Password={Configuration["DB_PASS"] };";
            var googleClientId = Configuration["GOOGLE_CLIENT_ID"];
            var googleClientSecret = Configuration["GOOGLE_SECRET_ID"];

            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });
            var redisConn =
                $"{Configuration["REDIS_HOST"] ?? "localhost"}:{Configuration["REDIS_PORT"] ?? "6379"},allowAdmin=true,password={Configuration["REDIS_PASSWORD"]}";
            var assemblyName = Assembly.GetAssembly(this.GetType())?.FullName;
            IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConn);

            services.AddSingleton(c => redis);

            // cookie policy to deal with temporary browser incompatibilities
            services.AddSameSiteCookiePolicy();

            services.Configure<DataProtectionTokenProviderOptions>(o => {
                o.TokenLifespan = TimeSpan.FromHours(24);
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(aspConnString, cfg => {
                    cfg.MigrationsAssembly(assemblyName);
                }));

            services
                .AddDataProtection(cfg => cfg.ApplicationDiscriminator = "id.godwit")
                .PersistKeysToStackExchangeRedis(redis)
                .ProtectKeysWithCertificate(certificate);

            services
                .AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            var builder = services
                .AddIdentityServer(options => {
                    options.Events.RaiseErrorEvents = true;
                    options.Events.RaiseInformationEvents = true;
                    options.Events.RaiseFailureEvents = true;
                    options.Events.RaiseSuccessEvents = true;

                    // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                    options.EmitStaticAudienceClaim = true;
                })
                    .AddConfigurationStore(options => {
                        options.ConfigureDbContext = cfg =>
                            cfg.UseNpgsql(cfgConnString, opt =>
                             opt.MigrationsAssembly(assemblyName));
                    })
                    // this adds the operational data from DB (codes, tokens, consents)
                    .AddOperationalStore(options => {
                        options.ConfigureDbContext = cfg =>
                            cfg.UseNpgsql(opsConnString, opt =>
                                opt.MigrationsAssembly(assemblyName));
                        // this enables automatic token cleanup. this is optional.
                        options.EnableTokenCleanup = true;
                    })
                .AddAspNetIdentity<ApplicationUser>()
                .AddInMemoryCaching()
                .AddClientStoreCache<ClientStore>()
                .AddResourceStoreCache<ResourceStore>()
                .AddConfigurationStoreCache();

            services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, UserClaimsPrincipalFactory>();

            // not recommended for production - you need to store your key material somewhere secure
            builder.AddSigningCredential(certificate);

            services.AddAuthentication()
                .AddGoogle(options => {
                    options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                    // register your IdentityServer with Google at https://console.developers.google.com
                    // enable the Google+ API
                    // set the redirect URI to https://localhost:5001/signin-google
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.AccessType = "offline";
                });
            services
                .AddHealthChecks()
                .AddNpgSql(aspConnString, name: "aspnet")
                .AddNpgSql(opsConnString, name: "ops")
                .AddNpgSql(cfgConnString, name: "cfg")
                .AddRedis(redis.Configuration);
        }

        public void Configure(IApplicationBuilder app) {
            app.UseForwardedHeaders();
            app.UseCookiePolicy();
            if (Environment.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            app.UseSerilogRequestLogging();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapHealthChecks("/hc");
            });
        }
    }
}