using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Text;

namespace ragApi
{
    public class Status
    {
        private readonly ILogger<Status> _logger;
        private readonly CosmosClient cosmosClient;
        private readonly string _databaseId = "ragdata";
        private readonly string _responseDbId = "responsedata";
        private readonly string _containerId = "rag";

        public Status(ILogger<Status> logger, CosmosClient cosmos)
        {
            _logger = logger;
            this.cosmosClient = cosmos;
        }

        [Function("Status")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{statusId}")] HttpRequest req,
            [DurableClient] DurableTaskClient durableClient,
            string statusId)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            Container container = cosmosClient.GetContainer(_databaseId, _responseDbId);
            var queryDef = new QueryDefinition(
                query: $"SELECT c.response,c.prompt,c.completed FROM c WHERE c.id=@id "
                ).WithParameter("@id", statusId);
            using FeedIterator<Object> feed = container.GetItemQueryIterator<Object>(
                queryDefinition: queryDef
            );
            StringBuilder sb = new StringBuilder();
            while (feed.HasMoreResults)
            {
                FeedResponse<Object> cosmosResponse = feed.ReadNextAsync().Result;
                foreach (Object item in cosmosResponse)
                {
                    sb.Append(JsonConvert.SerializeObject(item));

                }
            }
            _logger.LogInformation($" status for {statusId} {sb.ToString()}");
            return new OkObjectResult(sb.ToString());
        }
    }
}
