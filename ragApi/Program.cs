using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Embeddings;
using static System.Net.Mime.MediaTypeNames;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()

    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();       
        services.AddSingleton<EmbeddingClient>(provider =>
        {
            //var apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            return new EmbeddingClient("text-embedding-3-small", "<api_key>");
        });
        services.AddSingleton<ChatClient>(provider =>
        {
            //var apiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key");
            return new ChatClient("gpt-4o", "<api_key>");
        });
        services.AddSingleton<CosmosClient>(provider =>
        {
            var cosmosDbConnectionString = Environment.GetEnvironmentVariable("CosmosDB_ConnectionString");
            return new CosmosClient(@"https://inkragdb.documents.azure.com:443/", @"<cosmos_sas>");
        });
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            loggingBuilder.AddApplicationInsights();
        });
        services.Configure<FormOptions>(x =>
        {
            x.ValueLengthLimit = int.MaxValue;
            x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
        });
    })
.Build();

host.Run();

