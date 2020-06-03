using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

public class UpdateMeasurements
{ 
    private static IConfiguration configuration;
    private static TokenValidator tokenValidator;
    private const string audience = @"api://functionAPIDemo";
    private static string[] roles =  {"Data.ReadWrite"};
    private const string scopeName = "access_as_user";

    [FunctionName("UpdateVolcano")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
        [CosmosDB(
                databaseName: "VolcanoList",
                collectionName: "Volcano",
                ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<Volcano> volcanoHandler,
        ExecutionContext context,
        ILogger log)
    {
        var config = GetAzureADConfiguration(context);
        var tokenValidator = new TokenValidator(config, log);
        var claimsPrincipal = await tokenValidator.ValidateTokenAsync(req, audience);

        if(!tokenValidator.HasRightRolesAndScope(claimsPrincipal, scopeName, roles))
        {
            return new UnauthorizedResult();
        }
        var jsonString = await req.ReadAsStringAsync();
        var volcano = JsonConvert.DeserializeObject<Volcano>(jsonString);
        await volcanoHandler.AddAsync(volcano);

        return new OkObjectResult(volcano);
    }

    private static async Task<List<Volcano>> GetVolcanoData(DocumentClient client, string searchterm)
    {
        Uri collectionUri = UriFactory.CreateDocumentCollectionUri("VolcanoList", "Volcano");
        IDocumentQuery<Volcano> query = client.CreateDocumentQuery<Volcano>(collectionUri, new FeedOptions{EnableCrossPartitionQuery = true})
            .Where(p => p.VolcanoName == searchterm)
            .AsDocumentQuery();
        
        var result = new List<Volcano>();

        while (query.HasMoreResults)
        {
            foreach (Volcano volcano in await query.ExecuteNextAsync())
            {
                result.Add(volcano);
            }
        }

        return result;
    }

    private static IConfiguration GetAzureADConfiguration(ExecutionContext context)
    {
        var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

        return config.GetSection("AzureAd");
    }
}