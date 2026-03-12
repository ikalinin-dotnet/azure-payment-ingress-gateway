using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddAzureClients(clients =>
{
    var credential = new DefaultAzureCredential();

    // Passwordless Service Bus client
    var serviceBusNamespace = builder.Configuration["ServiceBusConnection__fullyQualifiedNamespace"]
        ?? throw new InvalidOperationException("ServiceBusConnection__fullyQualifiedNamespace is not configured.");

    clients.AddServiceBusClientWithNamespace(serviceBusNamespace)
           .WithCredential(credential);

    // Key Vault Secret client
    var keyVaultUri = builder.Configuration["KeyVaultUri"]
        ?? throw new InvalidOperationException("KeyVaultUri is not configured.");

    clients.AddSecretClient(new Uri(keyVaultUri))
           .WithCredential(credential);
});

await builder.Build().RunAsync();
