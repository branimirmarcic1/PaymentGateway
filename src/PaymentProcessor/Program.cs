
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PaymentProcessor;

var builder = Host.CreateApplicationBuilder(args);
BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));

builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(builder.Configuration.GetConnectionString("MongoDb")));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();