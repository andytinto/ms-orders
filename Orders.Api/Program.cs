using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Orders.Api.Middleware;
using Orders.Application;
using Orders.Application.Common.Interfaces;
using Orders.Application.Dtos;
using Orders.Infrastructure;
using Orders.Persistance;
using Serilog;
using Serilog.Events;

namespace Orders_Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ---------- Configuration ----------
            builder.Configuration
                .AddYamlFile("appsettings.yaml", optional: false, reloadOnChange: true)
                .AddYamlFile($"appsettings.{builder.Environment.EnvironmentName}.yaml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var config = builder.Configuration;

            // ---------- Basic values ----------
            var serviceName = config.GetValue<string>("Basic:ServiceName") ?? "MS-Orders";
            var environment = config.GetValue<string>("Basic:Environment") ?? builder.Environment.EnvironmentName;
            var allowedOrigin = config.GetValue<string>("BaseUrl:CPDExample") ?? "*";

            // ---------- Serilog Logging ----------
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Environment", environment)
                .Enrich.WithProperty("Application", serviceName)
                .WriteTo.Console()
                .CreateLogger();

            builder.Host.UseSerilog();

            // ---------- Options Pattern ----------
            builder.Services.Configure<BasicOptions>(config.GetSection("Basic"));
            builder.Services.Configure<ConnectionStringsOptions>(config.GetSection("ConnectionStrings"));
            builder.Services.Configure<RedisOptions>(config.GetSection("Redis"));
            builder.Services.Configure<MaskingOptions>(config.GetSection("Masking"));
            builder.Services.Configure<ExcludeLog>(config.GetSection("ExcludeLog"));

            // ---------- Layer Services Registration ----------
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddApplicationServices();
            builder.Services.AddInfrastructureService(config);
            builder.Services.AddPersistenceService();

            // ---------- Authentication / JWT Bearer ----------
            var jwtAccessKey = config.GetValue<string>("JwtAccess:Key") ?? "DefaultJwtKeyShouldBeLongEnough32Chars!";
            var jwtAccessAudience = config.GetValue<string>("JwtAccess:Audience");
            var jwtAccessIssuer = config.GetValue<string>("JwtAccess:Issuer");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrEmpty(jwtAccessIssuer),
                    ValidateAudience = !string.IsNullOrEmpty(jwtAccessAudience),
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtAccessIssuer,
                    ValidAudience = jwtAccessAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtAccessKey))
                };
            });

            builder.Services.AddAuthorization();

            // ---------- Controllers & Swagger ----------
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(option =>
            {
                option.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Orders API",
                    Version = "v1",
                    Description = "Orders Service API"
                });

                option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please enter JWT Bearer token format: Bearer {token}",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer"
                });

                option.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // ---------- CORS ----------
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("default", policy =>
                {
                    if (allowedOrigin == "*")
                    {
                        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    }
                    else
                    {
                        policy.WithOrigins(allowedOrigin).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
                    }
                });
            });

            // ---------- Build App ----------
            var app = builder.Build();

            // Custom Middleware Pipeline
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseMiddleware<RequestResponseLoggingMiddleware>();
            app.UseMiddleware<IdempotencyMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API v1");
                c.RoutePrefix = "swagger";
            });

            app.UseRouting();
            app.UseCors("default");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<CurrentUserMiddleware>();

            app.MapControllers();

            // Auto seed initial products if database is empty or running in-memory
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<IOrdersDbContext>();
                    if (!dbContext.Products.Any())
                    {
                        dbContext.Products.AddRange(
                            new Orders.Domain.Models.Product
                            {
                                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                                Name = "Product X (Distribution Item - Skenario A Test)",
                                StockQuantity = 15,
                                Price = 50000.00m,
                                RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
                                CreatedBy = "SYSTEM"
                            },
                            new Orders.Domain.Models.Product
                            {
                                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                                Name = "Product Y (Standard Distribution SKU)",
                                StockQuantity = 100,
                                Price = 25000.00m,
                                RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
                                CreatedBy = "SYSTEM"
                            },
                            new Orders.Domain.Models.Product
                            {
                                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                                Name = "Product Z (Limited Edition SKU)",
                                StockQuantity = 2,
                                Price = 150000.00m,
                                RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
                                CreatedBy = "SYSTEM"
                            }
                        );
                        dbContext.SaveChangesAsync().GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    Log.Information("Initial seed skipped: {Message}", ex.Message);
                }
            }

            app.Run();
        }
    }
}