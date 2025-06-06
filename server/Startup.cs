using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MySqlConnector;

namespace fairwayfinder;

public class Startup
{
  public Startup(IConfiguration configuration)
  {
    Configuration = configuration;

    // converts snake_case to PascalCase
    DefaultTypeMap.MatchNamesWithUnderscores = true;
  }

  public IConfiguration Configuration { get; }

  // This method gets called by the runtime. Use this method to add services to the container.
  public void ConfigureServices(IServiceCollection services)
  {
    ConfigureCors(services);
    ConfigureAuth(services);
    services.AddControllers();
    services.AddMemoryCache();
    services.AddSwaggerGen(c =>
    {
      c.SwaggerDoc("v1", new OpenApiInfo { Title = "fairwayfinder", Version = "v1" });
    });

    // Register HttpClient with cookie container and automatic decompression for GolfCourseService
    services.AddHttpClient<GolfCourseService>()
      .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
      {
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
      });

    services.AddSingleton<Auth0Provider>();
    services.AddScoped<IDbConnection>(_ => CreateDbConnection());
    services.AddScoped<AccountsRepository>();
    services.AddScoped<AccountService>();
    services.AddScoped<GolfCourseService>();
    services.AddScoped<GolfCourseRepository>();
  }

  private void ConfigureCors(IServiceCollection services)
  {
    services.AddCors(options =>
    {
      options.AddPolicy("CorsDevPolicy", builder =>
      {
        builder
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials()
          .WithOrigins("http://localhost:8080", "http://localhost:8081");
      });
    });
  }

  private void ConfigureAuth(IServiceCollection services)
  {
    services.AddAuthentication(options =>
    {
      options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
      options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
      options.Authority = $"https://{Configuration["AUTH0_DOMAIN"]}/";
      options.Audience = Configuration["AUTH0_AUDIENCE"];
    });
  }

  private IDbConnection CreateDbConnection()
  {
    string connectionString = Configuration["CONNECTION_STRING"];
    return new MySqlConnection(connectionString);
  }

  // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
  public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
  {
    if (env.IsDevelopment())
    {
      app.UseDeveloperExceptionPage();
      app.UseSwagger();
      app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "fairwayfinder"));
      app.UseCors("CorsDevPolicy");
    }

    app.UseHttpsRedirection();

    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();

    app.UseAuthorization();

    app.UseEndpoints(endpoints =>
    {
      endpoints.MapControllers();
    });
  }
}
