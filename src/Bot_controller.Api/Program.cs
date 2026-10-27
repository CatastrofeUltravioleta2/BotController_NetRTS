var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<BadgerOptions>(builder.Configuration.GetSection("BadgerBrawl"));
builder.Services.AddSingleton<BotService>();

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(c => c.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Run();