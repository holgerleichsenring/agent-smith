using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,        // <-- INTENTIONAL VULN: expired tokens accepted
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"]
        };
    });

builder.Services.AddControllers();
builder.Services.AddCors(opts => opts.AddDefaultPolicy(b =>
    b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));  // <-- INTENTIONAL VULN: wildcard CORS

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
// NOTE: app.UseHsts() intentionally missing
app.MapControllers();
app.Run();
