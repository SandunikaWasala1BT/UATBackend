using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.PowerPlatform.Dataverse.Client;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.DataverseRespository;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration.GetValue<string>("AzureBlobStorageConnectionString")));
//builder.Services.AddSingleton(x => new ServiceClient(builder.Configuration.GetValue<string>("DataverseConnectionString")));
builder.Services.AddScoped(sp => new ServiceClient(builder.Configuration.GetValue<string>("DataverseConnectionString")));
builder.Services.AddScoped<IBlobRepository, BlobRepository>();
builder.Services.AddScoped<IDataverseRepository, DataverseRepository>();

builder.Build().Run();
