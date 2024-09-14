using IntervalPulling.Rest.Api;
using IntervalPulling.Rest.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

builder.Services.AddScoped<IDataService, DataService>();

builder.Services.AddScoped<InMemoryCacheService>();

builder.Services.AddScoped<DistributedCacheService>();

builder.Services.AddScoped<DistributedCacheServiceChannels>();
builder.Services.AddHostedService<Processor>();
builder.Services
    .AddOptions<Configuration>()
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(policyBuilder => policyBuilder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

app.MapControllers();

app.Run();