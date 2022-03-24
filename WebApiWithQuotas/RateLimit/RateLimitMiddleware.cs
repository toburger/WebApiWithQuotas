using Microsoft.Extensions.Caching.Distributed;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

namespace WebApiWithQuotas.RateLimit
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedCache _cache;

        public RateLimitingMiddleware(RequestDelegate next, IDistributedCache cache)
        {
            _next = next;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, ISettings settings)
        {
            var ratelimitconfig = settings.RateLimitConfig;
            var endpoint = context.GetEndpoint();

   
            //var rateLimitingDecorator = endpoint?.Metadata.GetMetadata<LimitRequests>();

            //If no config present do nothing
            if (ratelimitconfig is null)
            {
                await _next(context);
                return;
            }

            //var key = GenerateClientKey(context);
            var (rlConfig, key) = GenerateClientKeyExtended(context, settings.RateLimitConfig);
            if (rlConfig is not null)
            {
                var clientStatistics = await GetClientStatisticsByKey(key);

                if (clientStatistics != null && DateTime.UtcNow < clientStatistics.LastSuccessfulResponseTime.AddSeconds(rlConfig.TimeWindow) && clientStatistics.NumberOfRequestsCompletedSuccessfully == rlConfig.MaxRequests)
                {
                    await context.Response.WriteAsJsonAsync(new QuotaExceededMessage { Message = "quota exceeded", RequestorType = rlConfig.Type });
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    return;
                }

                await UpdateClientStatisticsStorage(key, rlConfig.MaxRequests, TimeSpan.FromSeconds(rlConfig.TimeWindow));
            }

            await _next(context);
        }

        private static string GenerateClientKey(HttpContext context) => $"{context.Request.Path}_{context.Connection.RemoteIpAddress}";

        private static (RateLimitConfig? rlConfig, string key) GenerateClientKeyExtended(HttpContext context, List<RateLimitConfig> rlsettings)
        {
            var referer = "";

            //Check Referer
            if (context.Request.Headers.ContainsKey("Referer"))
                referer = context.Request.Headers["Referer"].ToString();
            else
            {
                //Search the QS for Referer
                if (context.Request.Query.ContainsKey("Referer"))
                    referer = context.Request.Query["Referer"].ToString();
            }

            var bearertoken = "";
            var loggeduser = "";
            //Check Referer
            if (context.Request.Headers.ContainsKey("Authorization"))
                bearertoken = context.Request.Headers["Authorization"].ToString();

            if(!string.IsNullOrEmpty(bearertoken) && bearertoken.StartsWith("Bearer"))
            {
                var handler = new JwtSecurityTokenHandler();
                var token = bearertoken.Replace("Bearer", "").Trim();

                var jwttoken = handler.ReadJwtToken(token);

                if(jwttoken != null)
                {
                    // Gets name from claims. Generally it's an email address.
                    var usernameClaim = jwttoken.Claims
                        .Where(x => x.Type == ClaimTypes.Name || x.Type == "name")
                        .FirstOrDefault();

                    if(usernameClaim != null)
                        loggeduser = usernameClaim.Value;
                }
            }

            //Check Loggeduser
            //TODO

            //TODO Check if User has Referer, isLogged isAnonymous


            return (referer, loggeduser) switch
            {
                //Referer passed generate key with Referer
                (string refererV, _) => (rlsettings.FirstOrDefault(x => x.Type == "Referer"), $"{refererV}"),
                //Logged user, decode token and use username as key
                (_, string loggeduserV) => (rlsettings.FirstOrDefault(x => x.Type == "Logged"), $"{loggeduserV}"),
                //Anonymous, Go to IP Restriction (Maybe on Path?)
                _ => (rlsettings.FirstOrDefault(x => x.Type == "Anonymous"), $"{context.Request.Path}_{context.Connection.RemoteIpAddress}"),
            };
        }

        private async Task<ClientStatistics?> GetClientStatisticsByKey(string key) => await _cache.GetCacheValueAsync<ClientStatistics>(key);

        private async Task UpdateClientStatisticsStorage(string key, int maxRequests, TimeSpan timeWindow)
        {
            var clientStat = await _cache.GetCacheValueAsync<ClientStatistics>(key);

            if (clientStat != null)
            {
                clientStat.LastSuccessfulResponseTime = DateTime.UtcNow;

                if (clientStat.NumberOfRequestsCompletedSuccessfully == maxRequests)
                    clientStat.NumberOfRequestsCompletedSuccessfully = 1;

                else
                    clientStat.NumberOfRequestsCompletedSuccessfully++;

                await _cache.SetCacheValueAsync<ClientStatistics>(key, timeWindow, clientStat);
            }
            else
            {
                var clientStatistics = new ClientStatistics
                {
                    LastSuccessfulResponseTime = DateTime.UtcNow,
                    NumberOfRequestsCompletedSuccessfully = 1
                };

                await _cache.SetCacheValueAsync(key, timeWindow, clientStatistics);
            }

        }
    }

    public class ClientStatistics
    {
        public DateTime LastSuccessfulResponseTime { get; set; }
        public int NumberOfRequestsCompletedSuccessfully { get; set; }
    }

    public class QuotaExceededMessage
    {
        public string? Message { get; set; }
        public string? MoreInfos { get; set; }
        public string? RequestorType { get; set; }
    }
}
