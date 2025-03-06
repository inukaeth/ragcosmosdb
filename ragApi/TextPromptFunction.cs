using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Microsoft.DurableTask;
using System.ClientModel;
using Microsoft.DurableTask.Client;
using Newtonsoft.Json;
using OpenAI.Embeddings;
using Microsoft.Azure.Cosmos;
using System.Text;
using System;
using Azure;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Identity.Client;
using Google.Protobuf;
using System.Text.Json.Serialization;




namespace ragApi
{
    public class TextPromptFunction
    {
        private readonly ILogger<TextPromptFunction> _logger;
        private readonly ChatClient chatClient;
        private readonly EmbeddingClient _openAIAPI;
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseId = "ragdata";
        private readonly string _responseDbId = "responsedata";
        private readonly string _containerId = "rag";


        public TextPromptFunction(ILogger<TextPromptFunction> logger, ChatClient chatClient, EmbeddingClient openAIAPI, CosmosClient cosmos)
        {
            this._logger = logger;
            this.chatClient = chatClient;
            _openAIAPI = openAIAPI;
            _cosmosClient = cosmos;

        }

        [Function("TextPromptFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function)] HttpRequest req,
            [DurableClient] DurableTaskClient durableClient
           )
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string prompt = data?.prompt;
            string instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync("TextOrchestratorFunction", prompt);
            req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            req.HttpContext.Response.Headers.Add("Content-Type", "application/json");
            _logger.LogInformation($"started chat with prompt {prompt} and instanceId {instanceId}");
            //      response.Headers.Add("Access-Control-Allow-Origin", "*"); // update this to your actual origin
            //response.Headers.Add("Access-Control-Allow-Methods", "GET,PUT,POST,DELETE");
            //response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Your-Custom-Header");
            return new OkObjectResult(instanceId);
            
        }

        [Function("TextOrchestratorFunction")]
        public async Task<string> OrchestratorFunction(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            //ChatClient client = new(model: "gpt-4o", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));          

            string prompt = context.GetInput<string>();
            //  context.SetCustomStatus("***** Request is complete  *******");
            ChatArgs arg = new ChatArgs { prompt = prompt, instanceId = context.InstanceId };


            string result = await context.CallActivityAsync<string>(nameof(StartChatStreamData), prompt );


            //AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = chatClient.CompleteChatStreamingAsync(messages);
            //await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            //{
            //    if (completionUpdate.ContentUpdate.Count > 0)
            //    {
            //        _logger.LogInformation(completionUpdate.ContentUpdate[0].Text);
            //        response = completionUpdate.ContentUpdate[0].Text;
            //        context.SetCustomStatus(JsonConvert.SerializeObject(response));
            //    }
            //}

            //await context.CallActivityAsync<string>(nameof(StartChatStreamData), messages);
            //while(result != null)
            //{
            //    string res = await context.CallActivityAsync<string>(nameof(GetChatStreamData), messages);
            //    if (res != null)
            //    {
            //        response += res;
            //        //context.SetCustomStatus(JsonConvert.SerializeObject(res));
            //    }
            //}
            //return response;

            //var result = chatClient.CompleteChat(messages);
            //context.SetCustomStatus(JsonConvert.SerializeObject(result.Value.Content.ToString
            context.SetCustomStatus(result);
            return result;


        }

        public class ChatArgs
        {
            [JsonPropertyName("prompt")]
            public string prompt;

            [JsonPropertyName("instanceId")]
            public string instanceId;
        }





        [Function(nameof(StartChatStreamData))]
        public async Task<string> StartChatStreamData([ActivityTrigger] string prompt, FunctionContext executionContext)
        {
            string instId = executionContext.BindingContext.BindingData["instanceId"].ToString(); ;
            Console.Write($"[ASSISTANT]: ");
            // get embeddings
            ReadOnlyMemory<float> embeddings = await GetEmbeddingsAsync(prompt);

            //query embeddings from Comsmos DB
            Container container = _cosmosClient.GetContainer(_databaseId, _containerId);
            var queryDef = new QueryDefinition(
                query: $"SELECT TOP 1 c.vector, VectorDistance(c.vector,@embedding) AS SimilarityScore FROM c ORDER BY VectorDistance(c.vector,@embedding)"
                ).WithParameter("@embedding", embeddings.ToArray());
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

            string matches = sb.ToString();

            List<ChatMessage> messages = new List<ChatMessage>();
            messages.Add(new SystemChatMessage($"Answer the question based only on the following context:\r\n\r\n            {matches}"));
            messages.Add(new UserChatMessage(prompt));
            string result = "";
            CollectionResult<StreamingChatCompletionUpdate> completionUpdates = chatClient.CompleteChatStreaming(messages);
            Container updateContainer = _cosmosClient.GetContainer(_databaseId, _responseDbId);
            List<string> response = new List<string>();            
            foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            {
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    _logger.LogInformation(completionUpdate.ContentUpdate[0].Text);
                    response.Add(completionUpdate.ContentUpdate[0].Text);
                    if(!String.IsNullOrEmpty(completionUpdate.ContentUpdate[0].Text))
                         await updateContainer.UpsertItemAsync<updateStatus>(new updateStatus { id= instId, invid = instId, prompt = prompt, response = response.ToArray(), completed=false }, new PartitionKey(instId));
                    // context.SetCustomStatus(JsonConvert.SerializeObject(response));
                }
            }
            await updateContainer.UpsertItemAsync<updateStatus>(new updateStatus { id= instId, invid = instId, prompt = prompt, response = response.ToArray(), completed = true }, new PartitionKey(instId));
            this._logger.LogInformation("Chat complete results are " + result);
            return result;

        }

       


        //[Function(nameof(GetChatStreamData))]
        //public async Task<string> GetChatStreamData([ActivityTrigger] List<ChatMessage> messages, FunctionContext executionContext)
        //{
        //    if(result == null) return null;
        //    string res = result.GetAsyncEnumerator().Current.ToString();
        //    bool isdone = await result.GetAsyncEnumerator().MoveNextAsync();
        //    if(isdone) result = null;
        //    return res;
        //}



        //[Function("TextOrchestratorFunction")]
        //public async Task<string> OrchestratorFunction(
        //    [OrchestrationTrigger] TaskOrchestrationContext context)
        //{
        //    //ChatClient client = new(model: "gpt-4o", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));          

        //    string prompt = context.GetInput<string>();


        //    Console.Write($"[ASSISTANT]: ");
        //    // get embeddings
        //    ReadOnlyMemory<float> embeddings = await GetEmbeddingsAsync(prompt);
        //    string matches = await QueryEmbeddings(embeddings.ToArray());
        //    List<ChatMessage> messages = new List<ChatMessage>();  
        //    messages.Add(new SystemChatMessage($"Answer the question based only on the following context:\r\n\r\n            {matches}"));
        //    messages.Add(new UserChatMessage(prompt));
        //    String response = "";
        //    AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = chatClient.CompleteChatStreamingAsync(messages);
        //    await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
        //    {
        //        if (completionUpdate.ContentUpdate.Count > 0)
        //        {
        //            _logger.LogInformation(completionUpdate.ContentUpdate[0].Text);
        //            response += completionUpdate.ContentUpdate[0].Text; 
        //        }
        //    }
        //    context.SetCustomStatus("***** Request is complete  *******");
        //    return response;


        //}

        //private async Task<string> QueryEmbeddings(float[] embeddings)
        //{
        //    var container = _cosmosClient.GetContainer(_databaseId, _containerId);
        //    var queryDef = new QueryDefinition(
        //query: $"SELECT TOP 1 c.vector, VectorDistance(c.vector,@embedding) AS SimilarityScore FROM c ORDER BY VectorDistance(c.vector,@embedding)"
        //).WithParameter("@embedding", embeddings);
        //    using FeedIterator<Object> feed = container.GetItemQueryIterator<Object>(
        //        queryDefinition: queryDef
        //    );            
        //    StringBuilder sb = new StringBuilder();
        //    while (feed.HasMoreResults)
        //    {
        //        FeedResponse<Object> response = await feed.ReadNextAsync();
        //        foreach (Object item in response)
        //        {
        //            sb.Append(JsonConvert.SerializeObject(item));                  
        //        }
        //    }
        //    return sb.ToString();
        //}



        private async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string prompt)
        {
            // Convert file content to string (assuming it's text data)          

            // Call OpenAI API to get embeddings
            OpenAIEmbedding result = await _openAIAPI.GenerateEmbeddingAsync(prompt);
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
    } 
}

