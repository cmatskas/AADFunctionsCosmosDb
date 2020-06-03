using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

public class TokenValidator
{
    private IConfiguration configuration;
    private ExecutionContext context;
    private ILogger log;
    private string token;

    private const string scopeType = @"http://schemas.microsoft.com/identity/claims/scope";

    public string Token {get {return token;} private set{token = value;}}
    
    public TokenValidator(IConfiguration config, ILogger logger, ExecutionContext cxt)
    {
        configuration = config;
        log = logger;
        context = cxt;
    }
   
    public void GetJwtFromHeader(HttpRequest req)
    {
        var authorizationHeader = req.Headers?["Authorization"];
        string[] parts = authorizationHeader?.ToString().Split(null) ?? new string[0];
        Token = (parts.Length == 2 && parts[0].Equals("Bearer")) ? parts[1] : string.Empty;
    }

    public async Task<ClaimsPrincipal> ValidateTokenAsync(HttpRequest req, string audience = "")
    {
        GetJwtFromHeader(req);
        if (string.IsNullOrEmpty(Token))
        {
            return null;
        }

        var jwtHandler = new JwtSecurityTokenHandler();
        var ConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                 $"https://login.microsoftonline.com/{configuration["Instance"]}/v2.0/.well-known/openid-configuration",
                 new OpenIdConnectConfigurationRetriever());
        var OIDconfig = await ConfigManager.GetConfigurationAsync();

        var tokenValidator = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidAudiences = new string[]{configuration["ClientId"],audience},
            ValidateAudience = true,
            IssuerSigningKeys = OIDconfig.SigningKeys,
            ValidIssuer = OIDconfig.Issuer
        };

        try
        {
            SecurityToken securityToken;
            var claimsPrincipal = tokenValidator.ValidateToken(token, validationParameters, out securityToken);
            return claimsPrincipal;
        }
        catch (Exception ex)
        {
            log.LogError(ex.ToString());
        }
        return null;
    }

    public bool HasRightRolesAndScope(ClaimsPrincipal claimsPrincipal, string scopeName, string[] roles = null)
    {
        bool isInRole = false;
        if(claimsPrincipal == null)
        {
            return false;
        }

        if(roles != null)
        {
            foreach(var role in roles)
            {
                if(claimsPrincipal.IsInRole(role))
                {
                    isInRole = true;
                }
            }
        }

        if(!isInRole)
        {
            return false;
        }

        var scopeClaim = claimsPrincipal.HasClaim(x => x.Type == scopeType)  
            ? claimsPrincipal.Claims.First(x => x.Type == scopeType).Value 
            : string.Empty;
        
        if(string.IsNullOrEmpty(scopeClaim))
        {
            return false;
        }

        if(!scopeClaim.Equals(scopeName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}