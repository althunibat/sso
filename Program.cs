// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Identity {
    public class Program {
        public static async Task<int> Main(string[] args) {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
                .Enrich.FromLogContext()
                // uncomment to write to Azure diagnostics stream
                //.WriteTo.File(
                //    @"D:\home\LogFiles\Application\identityserver.txt",
                //    fileSizeLimitBytes: 1_000_000,
                //    rollOnFileSizeLimit: true,
                //    shared: true,
                //    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Code)
                .CreateLogger();

            try {
                var seed = args.Contains("/seed");
                if (seed) {
                    args = args.Except(new[] { "/seed" }).ToArray();
                }

                var host = CreateHostBuilder(args).Build();

                if (seed) {
                    Log.Information("Seeding database...");
                    var config = host.Services.GetRequiredService<IConfiguration>();
                    var aspConnString = $"Server={config["DB_HOST"] ?? "localhost"};Port={config["DB_PORT"] ?? "5432"};Database={config["ASPNET_DB"] ?? "aspnet_db"};User Id={config["DB_USER"] ?? "postgres"};Password={config["DB_PASS"]};";
                    var cfgConnString = $"Server={config["DB_HOST"] ?? "localhost"};Port={config["DB_PORT"] ?? "5432"};Database={config["IDCFG_DB"] ?? "idcfg_db"};User Id={config["DB_USER"] ?? "postgres"};Password={config["DB_PASS"]};";
                    var opsConnString = $"Server={config["DB_HOST"] ?? "localhost"};Port={config["DB_PORT"] ?? "5432"};Database={config["IDOPS_DB"] ?? "idops_db"};User Id={config["DB_USER"] ?? "postgres"};Password={config["DB_PASS"]};";

                    await SeedData.EnsureSeedData(aspConnString, cfgConnString, opsConnString);
                    Log.Information("Done seeding database.");
                    return 0;
                }

                Log.Information("Starting host...");
                await host.RunAsync();
                return 0;
            }
            catch (Exception ex) {
                Log.Fatal(ex, "Host terminated unexpectedly.");
                return 1;
            }
            finally {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();
                });
    }
}