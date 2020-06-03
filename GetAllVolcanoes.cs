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
using System.Security.Claims;

public static class GetAllVolcanoes
{
    private static IConfiguration configuration;
    private static TokenValidator tokenValidator;
    private const string audience = @"api://functionAPIDemo";
    private const string scopeType = @"http://schemas.microsoft.com/identity/claims/scope";
    private static string[] roles = {"Data.Read", "Data.ReadWrite"};
    private const string scopeName = "access_as_user";

    [FunctionName("GetAllVolcanoes")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        [CosmosDB(
                databaseName: "VolcanoList",
                collectionName: "Volcano",
                ConnectionStringSetting = "CosmosDBConnection")] DocumentClient client,
        ExecutionContext context,
        ILogger log)
    {
        configuration = GetAzureADConfiguration(context);
        tokenValidator = new TokenValidator(configuration, log, context);
        var claimsPrincipal = await tokenValidator.ValidateTokenAsync(req, audience);

        if(!tokenValidator.HasRightRolesAndScope(claimsPrincipal, scopeName, roles))
        {
            return new UnauthorizedResult();
        }

        var result = await GetAllVolcanoData(client);
        return new OkObjectResult(result);
    }

    private static async Task<List<Volcano>> GetAllVolcanoData(DocumentClient client)
    {
        Uri collectionUri = UriFactory.CreateDocumentCollectionUri("VolcanoList", "Volcano");
        IDocumentQuery<Volcano> query = client.CreateDocumentQuery<Volcano>(collectionUri, new FeedOptions{EnableCrossPartitionQuery = true})
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

