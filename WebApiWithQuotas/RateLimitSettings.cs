namespace WebApiWithQuotas
{
    
    public class RateLimitConfig
    {
        public RateLimitConfig(string type, int timewindow, int maxrequests)
        {
            this.Type = type;
            this.TimeWindow = timewindow;
            this.MaxRequests = maxrequests;
        }

        public string Type { get; private set; }
        public int TimeWindow { get; private set; }
        public int MaxRequests { get; private set; }
    }

    public interface ISettings
    {      
        List<RateLimitConfig> RateLimitConfig { get; }        
    }

    public class RateLimitSettings : ISettings
    {
        private readonly IConfiguration configuration;    
        private readonly List<RateLimitConfig> rateLimitConfig;

        public RateLimitSettings(IConfiguration configuration)
        {
            this.configuration = configuration;

            var ratelimitlist = this.configuration.GetSection("RateLimitConfig").GetChildren();
            this.rateLimitConfig = new List<RateLimitConfig>();
            foreach (var ratelimitconfig in ratelimitlist)
            {
                this.rateLimitConfig.Add(new RateLimitConfig(ratelimitconfig.GetValue<string>("Type", ""), ratelimitconfig.GetValue<int>("TimeWindow", 0), ratelimitconfig.GetValue<int>("MaxRequests", 0)));
            }
        }
     
        public List<RateLimitConfig> RateLimitConfig => this.rateLimitConfig;
    }
}
