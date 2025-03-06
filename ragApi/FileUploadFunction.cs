using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using OpenAI.Embeddings;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig;
using System.Text;
using System.ComponentModel;


namespace ragApi
{
    public class FileUploadFunction
    {
        private readonly ILogger<FileUploadFunction> _logger;
        private readonly EmbeddingClient _openAIAPI;
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseId = "ragdata";
        private readonly string _containerId = "rag";

        public FileUploadFunction(ILogger<FileUploadFunction> logger, EmbeddingClient openAIAPI, CosmosClient cosmosClient)
        {
            _logger = logger;
            _openAIAPI = openAIAPI;
            _cosmosClient = cosmosClient;
        }

        [Function("FileUploadFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
            [DurableClient] DurableTaskClient starter,
            FunctionContext executionContext)
        {
            try
            {
                _logger.LogInformation("C# HTTP trigger function processed a request.");

                // Check if the request contains a file
                if (!req.HasFormContentType || req.Form.Files.Count == 0)
                {
                    return new BadRequestObjectResult("Please upload a file.");
                }

                var file = req.Form.Files[0];

                // Read the file content
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    var fileContent = stream.ToArray();
                    starter.ScheduleNewOrchestrationInstanceAsync("OrchestratorFunction", fileContent);
                    return new OkObjectResult("started activity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception occured " + ex.Message);
                return new BadRequestResult();
            }
        }

        [Function("OrchestratorFunction")]
        public async Task OrchestratorFunction(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var fileContent = context.GetInput<byte[]>();
            using (var document = PdfDocument.Open(fileContent))
            {
                foreach (var page in document.GetPages())
                {
                    var text = ContentOrderTextExtractor.GetText(page, true);                    
                    context.CallActivityAsync<string>("ActivityFunction", text);                  

                }
            }
           
        }

        [Function("ActivityFunction")]
        public async Task ActivityFunction([ActivityTrigger] string values)
        {
           
            _logger.LogInformation("Performing background work with the uploaded file.");

            // Call OpenAI API to get embeddings
            var embeddings = await GetEmbeddingsAsync(Encoding.UTF8.GetBytes(values));

            // Write embeddings to Cosmos DB
            await WriteEmbeddingsToCosmosDbAsync(embeddings);
        }



        private async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(byte[] fileContent)
        {
            // Convert file content to string (assuming it's text data)
            var fileText = System.Text.Encoding.UTF8.GetString(fileContent);

            // Call OpenAI API to get embeddings
             OpenAIEmbedding result = await _openAIAPI.GenerateEmbeddingAsync(fileText);
            /* {
                 Input = fileText,
                 Model = "text-embedding-ada-002"
             }); */

            ReadOnlyMemory<float> vector = result.ToFloats();
            //float[] val = new float[3];
            //val[0] = 1.0f;
            //val[1] = 2.0f;
            //val[2] = 3.0f;
            //ReadOnlyMemory<float> vector = new ReadOnlyMemory<float>(val);            
            return vector;
        }

        private async Task WriteEmbeddingsToCosmosDbAsync(ReadOnlyMemory<float> embeddings)
        {
            var container = _cosmosClient.GetContainer(_databaseId, _containerId);
            var embeddingDocument = new
            {
                id = Guid.NewGuid().ToString(),
                userId = System.Guid.NewGuid().ToString(),
                vector = embeddings.ToArray()
            };

            await container.CreateItemAsync(embeddingDocument, new PartitionKey(embeddingDocument.userId));
            _logger.LogInformation("Embeddings written to Cosmos DB.");
        }
    }
}
