using System;

namespace MediaBrowser4.DB.Api
{
    public sealed class ApiConnectionOptions
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Tenant { get; set; }
        public int TimeoutSeconds { get; set; }

        public ApiConnectionOptions()
        {
            this.BaseUrl = "http://localhost:8080";
            this.TimeoutSeconds = 60;
        }

        public string BuildApiBaseUrl()
        {
            if (String.IsNullOrWhiteSpace(this.BaseUrl))
                throw new InvalidOperationException("Die API-Basis-URL fehlt.");

            string baseUrl = this.BaseUrl.Trim();

            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Die API-Basis-URL muss mit http:// oder https:// beginnen.");
            }

            return baseUrl.TrimEnd('/') + "/api/v1";
        }
    }
}
