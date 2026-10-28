using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MediaBrowser4.DB.Api
{
    public sealed class ApiException : Exception
    {
        public ApiException(string message, int statusCode, string responseBody)
            : base(message)
        {
            this.StatusCode = statusCode;
            this.ResponseBody = responseBody;
        }

        public int StatusCode { get; private set; }
        public string ResponseBody { get; private set; }
    }

    internal sealed class SpringApiClient
    {
        private readonly HttpClient httpClient;
        private readonly string apiBaseUrl;
        private readonly JsonSerializerSettings serializerSettings;
        private readonly ApiConnectionOptions options;
        private readonly object authSync = new object();
        private readonly bool useApiKey;

        private string accessToken;
        private string refreshToken;
        private DateTime accessTokenExpiresUtc = DateTime.MinValue;

        public SpringApiClient(ApiConnectionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");

            this.options = options;
            this.apiBaseUrl = options.BuildApiBaseUrl();
            this.useApiKey = !String.IsNullOrWhiteSpace(options.ApiKey);
            this.httpClient = new HttpClient();
            this.httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
            this.httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            this.serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
                DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };
        }

        public T Get<T>(string path, IDictionary<string, string> query = null)
        {
            return this.Deserialize<T>(this.Execute(HttpMethod.Get, path, query, null, false));
        }

        public T GetOrDefault<T>(string path, IDictionary<string, string> query = null)
        {
            ApiResponse response = this.Execute(HttpMethod.Get, path, query, null, true);

            if (response.StatusCode == 404 || response.Body == null)
                return default(T);

            return this.Deserialize<T>(response);
        }

        public T Post<T>(string path, object body = null)
        {
            return this.Deserialize<T>(this.Execute(HttpMethod.Post, path, null, body, false));
        }

        public void Post(string path, object body = null)
        {
            this.Execute(HttpMethod.Post, path, null, body, false);
        }

        public T Put<T>(string path, object body = null)
        {
            return this.Deserialize<T>(this.Execute(HttpMethod.Put, path, null, body, false));
        }

        public void Put(string path, object body = null)
        {
            this.Execute(HttpMethod.Put, path, null, body, false);
        }

        public void Delete(string path, IDictionary<string, string> query = null)
        {
            this.Execute(HttpMethod.Delete, path, query, null, false);
        }

        public byte[] GetBytes(string path, IDictionary<string, string> query = null)
        {
            ApiResponse response = this.Execute(HttpMethod.Get, path, query, null, true);

            if (response.StatusCode == 404)
                return null;

            return response.Binary;
        }

        private T Deserialize<T>(ApiResponse response)
        {
            if (response == null || response.Body == null)
                return default(T);

            return JsonConvert.DeserializeObject<T>(response.Body, this.serializerSettings);
        }

        private ApiResponse Execute(HttpMethod method, string path, IDictionary<string, string> query,
            object body, bool allowNotFound)
        {
            ApiRequestInfo requestInfo = new ApiRequestInfo
            {
                Method = method.Method,
                Path = path
            };

            IApiRequestObserver observer = ApiRequestObserverHost.Observer;

            if (observer != null)
            {
                return observer.Execute(requestInfo, cancellationToken =>
                    this.ExecuteCore(method, path, query, body, allowNotFound, cancellationToken));
            }

            return this.ExecuteCore(method, path, query, body, allowNotFound, CancellationToken.None);
        }

        private ApiResponse ExecuteCore(HttpMethod method, string path, IDictionary<string, string> query,
            object body, bool allowNotFound, CancellationToken cancellationToken)
        {
            string url = this.BuildUrl(path, query);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string token = this.EnsureAccessToken(cancellationToken);
                ApiResponse response = this.SendOnce(method, url, body, token, cancellationToken);

                if (response.StatusCode == (int)HttpStatusCode.Unauthorized
                    && attempt == 0 && !this.useApiKey && !String.IsNullOrWhiteSpace(token))
                {
                    this.InvalidateAccessToken();
                    continue;
                }

                if (response.StatusCode >= 400
                    && !(allowNotFound && response.StatusCode == (int)HttpStatusCode.NotFound))
                {
                    throw new ApiException(
                        "API-Aufruf fehlgeschlagen: " + method.Method + " " + url
                        + " (" + response.StatusCode + " " + response.ReasonPhrase + ")"
                        + (String.IsNullOrWhiteSpace(response.Body) ? String.Empty : Environment.NewLine + response.Body),
                        response.StatusCode, response.Body);
                }

                return response;
            }

            throw new ApiException("API-Aufruf fehlgeschlagen: " + method.Method + " " + url,
                (int)HttpStatusCode.Unauthorized, null);
        }

        private ApiResponse SendOnce(HttpMethod method, string url, object body, string bearerToken,
            CancellationToken cancellationToken)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(method, url))
            {
                if (!String.IsNullOrWhiteSpace(bearerToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                if (body != null)
                {
                    string json = JsonConvert.SerializeObject(body, this.serializerSettings);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using (HttpResponseMessage response = this.httpClient.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false).GetAwaiter().GetResult())
                {
                    byte[] binary = response.Content.ReadAsByteArrayAsync()
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    string text = binary.Length == 0 ? null : Encoding.UTF8.GetString(binary);

                    return new ApiResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        ReasonPhrase = response.ReasonPhrase,
                        Body = text,
                        Binary = binary
                    };
                }
            }
        }

        private string EnsureAccessToken(CancellationToken cancellationToken)
        {
            if (this.useApiKey)
                return this.options.ApiKey.Trim();

            if (String.IsNullOrWhiteSpace(this.options.Username))
                return null;

            lock (this.authSync)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!String.IsNullOrWhiteSpace(this.accessToken)
                    && DateTime.UtcNow < this.accessTokenExpiresUtc)
                {
                    return this.accessToken;
                }

                if (!String.IsNullOrWhiteSpace(this.refreshToken))
                {
                    try
                    {
                        return this.RefreshAccessToken(cancellationToken);
                    }
                    catch (ApiException ex)
                    {
                        if (ex.StatusCode != (int)HttpStatusCode.BadRequest
                            && ex.StatusCode != (int)HttpStatusCode.Unauthorized)
                        {
                            throw;
                        }

                        this.accessToken = null;
                        this.refreshToken = null;
                        this.accessTokenExpiresUtc = DateTime.MinValue;
                    }
                }

                return this.Login(cancellationToken);
            }
        }

        private string Login(CancellationToken cancellationToken)
        {
            string loginUrl = this.BuildUrl("/auth/login", null);
            ApiResponse response = this.SendOnce(HttpMethod.Post,
                loginUrl,
                new ApiLoginRequestDto
                {
                    Username = this.options.Username,
                    Password = this.options.Password,
                    Tenant = this.options.Tenant
                },
                null,
                cancellationToken);

            this.EnsureSuccess(response, HttpMethod.Post, loginUrl);

            ApiTokenDto token = this.Deserialize<ApiTokenDto>(response);

            if (token == null || String.IsNullOrWhiteSpace(token.AccessToken))
                throw new ApiException("Die API hat kein gültiges Access-Token geliefert.",
                    response.StatusCode, response.Body);

            this.ApplyToken(token);
            return this.accessToken;
        }

        private string RefreshAccessToken(CancellationToken cancellationToken)
        {
            string previousRefreshToken = this.refreshToken;
            string refreshUrl = this.BuildUrl("/auth/refresh", null);
            ApiResponse response = this.SendOnce(HttpMethod.Post,
                refreshUrl,
                new ApiRefreshTokenDto { RefreshToken = previousRefreshToken },
                null,
                cancellationToken);

            this.EnsureSuccess(response, HttpMethod.Post, refreshUrl);

            ApiTokenDto token = this.Deserialize<ApiTokenDto>(response);

            if (token == null || String.IsNullOrWhiteSpace(token.AccessToken))
                throw new ApiException("Die API hat kein gültiges Access-Token geliefert.",
                    response.StatusCode, response.Body);

            if (String.IsNullOrWhiteSpace(token.RefreshToken))
                token.RefreshToken = previousRefreshToken;

            this.ApplyToken(token);
            return this.accessToken;
        }

        private void ApplyToken(ApiTokenDto token)
        {
            int expiresIn = token.ExpiresIn > 0 ? token.ExpiresIn : 600;
            this.accessToken = token.AccessToken;
            this.refreshToken = token.RefreshToken;
            this.accessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, expiresIn - 30));
        }

        private void InvalidateAccessToken()
        {
            lock (this.authSync)
            {
                this.accessToken = null;
                this.accessTokenExpiresUtc = DateTime.MinValue;
            }
        }

        private void EnsureSuccess(ApiResponse response, HttpMethod method, string url)
        {
            if (response.StatusCode >= 400)
            {
                throw new ApiException(
                    "API-Aufruf fehlgeschlagen: " + method.Method + " " + url
                    + " (" + response.StatusCode + " " + response.ReasonPhrase + ")"
                    + (String.IsNullOrWhiteSpace(response.Body) ? String.Empty : Environment.NewLine + response.Body),
                    response.StatusCode, response.Body);
            }
        }

        private string BuildUrl(string path, IDictionary<string, string> query)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(this.apiBaseUrl);
            builder.Append(path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path);

            if (query != null && query.Count > 0)
            {
                bool first = true;

                foreach (KeyValuePair<string, string> entry in query)
                {
                    if (entry.Value == null)
                        continue;

                    builder.Append(first ? "?" : "&");
                    builder.Append(Uri.EscapeDataString(entry.Key));
                    builder.Append("=");
                    builder.Append(Uri.EscapeDataString(entry.Value));
                    first = false;
                }
            }

            return builder.ToString();
        }

        private sealed class ApiResponse
        {
            public int StatusCode { get; set; }
            public string ReasonPhrase { get; set; }
            public string Body { get; set; }
            public byte[] Binary { get; set; }
        }
    }
}
