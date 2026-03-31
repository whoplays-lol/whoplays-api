using WannaFill.API.Hubs;
using WannaFill.API.Services;
using WannaFill.API.Stores;

var builder = WebApplication.CreateBuilder(args);

// In-memory stores — Singleton so state survives across requests
builder.Services.AddSingleton<InMemoryQueueStore>();
builder.Services.AddSingleton<InMemoryMatchStore>();
builder.Services.AddSingleton<InMemoryChatStore>();

// Services — Scoped (stateless, depend on singleton stores)
builder.Services.AddScoped<IMatchmakingService, MatchmakingService>();
builder.Services.AddScoped<IMessageService, MessageService>();

// SignalR
builder.Services.AddSignalR();

// CORS — env var takes precedence (production), fallback to config or dev default
var allowedOriginsEnv = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
var allowedOrigins = !string.IsNullOrEmpty(allowedOriginsEnv)
    ? allowedOriginsEnv.Split(',')
    : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
      ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WannaFill API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MatchmakingHub>("/hubs/matchmaking");

// Serve static frontend files (production)
app.UseDefaultFiles();
app.UseStaticFiles();

// SPA fallback: for any non-API, non-hub route, serve index.html
app.MapFallbackToFile("index.html");

app.Run();
