using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MediaBrowser4.DB.API
{
    /// <summary>
    /// A helper class to communicate with the MediaFile API for inserting media files.
    /// </summary>
    public class MediaFileApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaFileApiClient"/> class.
        /// </summary>
        /// <param name="baseUrl">The base URL of the API (e.g., "http://localhost:5000").</param>
        /// <param name="username">The username for Basic Authentication.</param>
        /// <param name="password">The password for Basic Authentication.</param>
        public MediaFileApiClient(string baseUrl, string username, string password)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient();

            // Set up Basic Authentication header
            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
        }

        /// <summary>

        /// Sends a MediafileInsertDto to the API to create a new media file asynchronously.
        /// </summary>
        /// <param name="mediaFileDto">The data transfer object containing the media file information.</param>
        /// <returns>True if the request was successful; otherwise, false.</returns>
        public async Task<bool> InsertMediaFileAsync(MediafileInsertDto mediaFileDto)
        {
            try
            {   
                var jsonContent = ManualJsonConverter.CreateJsonFromDto(mediaFileDto); // Manuelle JSON-Erstellung
           
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Construct the full API endpoint URL
                var requestUrl = $"{_baseUrl}/api/mediafile";

                // Send the POST request
                var response = await _httpClient.PostAsync(requestUrl, httpContent);

                // Return true if the response indicates success (e.g., 200 OK, 201 Created)
                return response.IsSuccessStatusCode;
            }
            catch (TypeInitializationException ex)
            {
                // Halte hier mit einem Breakpoint an und schau dir 'ex.InnerException' an.
                // Oder logge die innere Ausnahme:
                Console.WriteLine(ex.InnerException);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"An error occurred while inserting the media file: {ex.Message}");
               
            }
            return false;
        }
    }
}