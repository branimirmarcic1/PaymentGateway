using Confluent.Kafka;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Konfiguracija Redis-a
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis"), true);
    return ConnectionMultiplexer.Connect(configuration);
});

// Konfiguracija Kafka Producenta
builder.Services.AddSingleton<IProducer<Null, string>>(sp =>
{
    var config = new ProducerConfig { BootstrapServers = builder.Configuration.GetConnectionString("Kafka") };
    return new ProducerBuilder<Null, string>(config).Build();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();