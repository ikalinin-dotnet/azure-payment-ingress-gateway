using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton(sp =>
{
    var endpoint = builder.Configuration["CosmosDbEndpoint"]
        ?? throw new InvalidOperationException("CosmosDbEndpoint is not configured.");

    return new CosmosClient(endpoint, new DefaultAzureCredential(), new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    });
});

await builder.Build().RunAsync();
