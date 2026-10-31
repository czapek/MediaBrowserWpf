using ApiMock;
using ApiMock.Shared;
using MediaBrowser4.DB.Api;

string[] startupArgs = args;
string port = GetArg(startupArgs, "--port", "8282");
string listen = GetArg(startupArgs, "--listen", "localhost");
string urls = GetArg(startupArgs, "--urls",
    String.Equals(listen, "any", StringComparison.OrdinalIgnoreCase)
        ? "http://0.0.0.0:" + port
        : "http://localhost:" + port);
bool verbose = startupArgs.Any(a => String.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase));

MockStore store = MockStore.Load(startupArgs);

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = startupArgs,
    ContentRootPath = AppContext.BaseDirectory
});

builder.WebHost.UseUrls(urls);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Logging.SetMinimumLevel(verbose ? LogLevel.Information : LogLevel.Warning);

WebApplication app = builder.Build();

if (verbose)
{
    app.Use(async (context, next) =>
    {
        Console.WriteLine(context.Request.Method + " " + context.Request.Path + context.Request.QueryString);
        await next();
    });
}

app.Use(async (context, next) =>
{
    string path = context.Request.Path.Value ?? "";

    if (path.StartsWith("/api/v1/auth/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    string authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
        String.IsNullOrWhiteSpace(authorization.Substring("Bearer ".Length)))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapPost("/api/v1/auth/login", (ApiLoginRequestDto request) =>
{
    return Results.Json(new ApiTokenDto
    {
        AccessToken = "mock-access-token",
        RefreshToken = "mock-refresh-token",
        TokenType = "Bearer",
        ExpiresIn = 86400
    });
});

app.MapPost("/api/v1/auth/refresh", (ApiRefreshTokenDto request) =>
{
    return Results.Json(new ApiTokenDto
    {
        AccessToken = "mock-access-token",
        RefreshToken = String.IsNullOrWhiteSpace(request?.RefreshToken) ? "mock-refresh-token" : request.RefreshToken,
        TokenType = "Bearer",
        ExpiresIn = 86400
    });
});

app.MapPost("/api/v1/auth/logout", () => Results.NoContent());

app.MapGet("/api/v1/catalog/validate", () => Results.Json(store.GetCatalogInfo()));
app.MapPost("/api/v1/catalog/create", () => Results.NoContent());
app.MapGet("/api/v1/catalog/properties", () => Results.Json(store.GetProperties()));
app.MapPut("/api/v1/catalog/properties", (Dictionary<string, string> properties) =>
{
    store.SaveProperties(properties);
    return Results.NoContent();
});
app.MapGet("/api/v1/catalog/last-folders", () => Results.Json(store.GetLastFolders()));
app.MapGet("/api/v1/catalog/capabilities", () =>
{
    CapabilitiesSeed capabilities = store.Seed.Capabilities;
    return Results.Json(new
    {
        canReadContent = capabilities.CanReadContent,
        canStreamRange = capabilities.CanStreamRange,
        canReadAttachments = capabilities.CanReadAttachments,
        canReadSidecars = capabilities.CanReadSidecars,
        canWriteCatalog = capabilities.CanWriteCatalog,
        canUploadContent = capabilities.CanUploadContent,
        canModifyFiles = capabilities.CanModifyFiles,
        contentStoreConfigured = capabilities.ContentStoreConfigured
    });
});

app.MapGet("/api/v1/folders", () => Results.Json(store.GetFolders()));
app.MapPost("/api/v1/folders/ensure", (ApiFolderDto folder) => Results.Json(store.EnsureFolder(folder)));
app.MapDelete("/api/v1/folders/{id:int}", (int id) =>
{
    store.RemoveFolder(id);
    return Results.NoContent();
});
app.MapGet("/api/v1/folders/{id:int}/count", (int id) => Results.Json(store.CountFolderItems(id)));

app.MapGet("/api/v1/categories/tree", () => Results.Json(store.GetCategoryTree()));
app.MapGet("/api/v1/categories", (int variationId) => Results.Json(store.GetCategoriesForVariation(variationId)));
app.MapPost("/api/v1/categories", (ApiCategoryDto category) => Results.Json(store.SaveCategory(category)));
app.MapPost("/api/v1/categories/by-path", (ApiPathDto path) =>
    Results.Json(store.SaveCategoryByPath(path?.Path, path?.SortPath)));
app.MapDelete("/api/v1/categories/{id:int}", (int id) =>
{
    store.RemoveCategory(id);
    return Results.NoContent();
});
app.MapGet("/api/v1/categories/geo", (string kind, double longitude, double width, double latitude,
    double height) => Results.Json(store.GetGeoCategories(kind, longitude, width, latitude, height)));
app.MapPost("/api/v1/categories/from-media", (List<int> variationIds) =>
    Results.Json(store.GetCategoriesFromMedia(variationIds)));
app.MapGet("/api/v1/categories/identifiers/{variationId:int}", (int variationId) =>
    Results.Json(store.GetCategoryIdentifiers(variationId)));

app.MapGet("/api/v1/roles", () => Results.Json(store.GetRoles()));

app.MapPost("/api/v1/mediafiles/query", (ApiMediaQueryDto query) => Results.Json(store.QueryMedia(query)));

app.MapPost("/api/v1/mediafiles/geo/clusters", IResult (GeoClusterQueryInput query) =>
{
    ApiGeoClusterResultDto result = store.GetGeoClusters(query, out string error);
    return error != null ? Results.BadRequest(new { error }) : Results.Json(result);
});
app.MapPost("/api/v1/mediafiles/geo/items", IResult (GeoItemsQueryInput query) =>
{
    List<ApiMediaItemDto> result = store.GetGeoItems(query, out string error);
    return error != null ? Results.BadRequest(new { error }) : Results.Json(result);
});

app.MapPost("/api/v1/mediafiles/sql-query", (ApiSqlQueryDto query) =>
    Results.Json(store.ExecuteSqlQuery(query)));
app.MapGet("/api/v1/mediafiles/sql-request", () => Results.Json(store.GetSampleSqlRequest()));
app.MapGet("/api/v1/mediafiles/{id:int}", (int id) =>
{
    ApiMediaItemDto item = store.GetMedia(id);
    return item == null ? Results.NotFound() : Results.Json(item);
});
app.MapPut("/api/v1/mediafiles/{id:int}", (int id, ApiMediaItemDto item) =>
{
    store.UpdateMedia(id, item);
    return Results.NoContent();
});

app.MapMethods("/api/v1/mediafiles/{id:int}/content", new[] { "GET", "HEAD" }, (HttpContext context, int id) =>
{
    byte[] bytes = store.GetContentBytes(id);
    if (bytes == null || bytes.Length == 0)
        return Results.NotFound();

    string contentType = store.GetContentType(id);
    return Results.File(bytes, contentType, enableRangeProcessing: true);
});

app.MapGet("/api/v1/mediafiles/{id:int}/sidecars", (int id) => Results.Json(new List<object>()));
app.MapGet("/api/v1/mediafiles/{id:int}/description", (int id) =>
{
    string value = store.GetMediaDescription(id);
    return value == null ? Results.NotFound() : Results.Json(new ApiTextDto { Value = value });
});

app.MapGet("/api/v1/mediafiles/{id:int}/variations", (int id, bool includeThumbs) =>
    Results.Json(store.GetVariations(id, includeThumbs)));
app.MapPost("/api/v1/mediafiles/{id:int}/variations", (int id, ApiVariationCreateDto variation) =>
    Results.Json(store.CreateVariation(id, variation)));
app.MapDelete("/api/v1/mediafiles/{id:int}/variations", (int id, string name) =>
{
    store.RemoveVariation(id, name);
    return Results.NoContent();
});
app.MapPost("/api/v1/mediafiles/{id:int}/variations/replace", (int id, List<ApiVariationDto> variations) =>
    Results.Json(store.ReplaceVariations(id, variations)));

app.MapPost("/api/v1/mediafiles/actions", (ApiMediaActionDto action) =>
{
    store.ApplyAction(action);
    return Results.NoContent();
});

app.MapPost("/api/v1/mediafiles/thumbs", (List<int> variationIds) =>
    Results.Json(store.GetThumbs(variationIds)));

app.MapGet("/api/v1/mediafiles/checksum-count", (string checksum) =>
{
    List<ApiMediaItemDto> matches = store.QueryMedia(new ApiMediaQueryDto
    {
        Mode = "FILES_FROM_CHECKSUM",
        Checksum = checksum,
        Limit = 1000
    });
    return Results.Json(matches.Count);
});

app.MapPost("/api/v1/mediafiles/from-xml", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
app.MapGet("/api/v1/mediafiles/{id:int}/image-analysis", (int id) => Results.NotFound());

app.MapPost("/api/v1/image-analysis/query", () => Results.Json(new List<object>()));
app.MapPost("/api/v1/image-analysis/search", () =>
    Results.Json(new ApiImageAnalysisSearchResultDto
    {
        Total = 0,
        Offset = 0,
        Limit = 100,
        Items = new List<ApiImageAnalysisMediaItemDto>()
    }));
app.MapGet("/api/v1/image-analysis/value-search", () =>
    Results.Json(new ApiImageAnalysisValueSearchResultDto { Values = new List<string>() }));
app.MapGet("/api/v1/image-analysis/filter-fields", () =>
    Results.Json(new List<ApiImageAnalysisFilterFieldDto>()));
app.MapGet("/api/v1/image-analysis/ranges", () => Results.Json(new ApiImageAnalysisRangesDto()));

app.MapGet("/api/v1/variations/{id:int}", (int id) =>
{
    ApiVariationDto variation = store.GetVariation(id);
    return variation == null ? Results.NotFound() : Results.Json(variation);
});
app.MapPut("/api/v1/variations/{id:int}", (int id, ApiVariationDto variation) =>
{
    ApiVariationDto result = store.UpdateVariation(id, variation);
    return result == null ? Results.NotFound() : Results.Json(result);
});
app.MapDelete("/api/v1/variations/{id:int}", (int id) =>
{
    store.RemoveVariation(id);
    return Results.NoContent();
});
app.MapPost("/api/v1/variations/{id:int}/default", (int id) =>
{
    ApiVariationDto variation = store.GetVariation(id);
    if (variation == null)
        return Results.NotFound();

    store.SetDefaultVariation(variation.MediaItemId, id);
    return Results.NoContent();
});
app.MapGet("/api/v1/variations/{id:int}/layers", (int id) => Results.Json(store.GetLayers(id)));
app.MapPut("/api/v1/variations/{id:int}/layers", (int id, List<ApiLayerDto> layers) =>
{
    store.SetLayers(id, layers);
    return Results.NoContent();
});
app.MapGet("/api/v1/variations/{id:int}/thumb", (int id) =>
{
    byte[] bytes = store.GetThumbBytes(id);
    return bytes == null || bytes.Length == 0 ? Results.NotFound() : Results.File(bytes, "image/jpeg");
});
app.MapPut("/api/v1/variations/{id:int}/thumb", (int id, ApiThumbUploadDto thumb) =>
{
    store.SetThumb(id, thumb);
    return Results.NoContent();
});

app.MapGet("/api/v1/previews/{variationId:int}", (int variationId, int width, int height) =>
{
    ApiPreviewDto preview = store.GetPreview(variationId);
    return preview == null ? Results.NotFound() : Results.Json(preview);
});
app.MapGet("/api/v1/previews/exists", (int variationId, int width, int height) =>
    Results.Json(store.PreviewExists(variationId)));
app.MapGet("/api/v1/previews/variation-ids", (int width, int height) =>
    Results.Json(store.GetPreviewVariationIds()));

app.MapPost("/api/v1/metadata/query", (List<int> mediaItemIds) =>
    Results.Json(store.GetMetadata(mediaItemIds)));
app.MapGet("/api/v1/metadata/keys", () => Results.Json(store.GetMetadataKeys()));

app.MapPost("/api/v1/descriptions/query", (List<int> mediaItemIds) =>
    Results.Json(store.GetDescriptions(mediaItemIds)));
app.MapPost("/api/v1/descriptions", (ApiDescriptionDto description) =>
{
    store.SaveDescription(description);
    return Results.NoContent();
});

app.MapPost("/api/v1/attachments/query", (List<int> mediaItemIds) =>
    Results.Json(new List<ApiAttachmentDto>()));
app.MapGet("/api/v1/attachments/{id:int}/content", (int id) => Results.NotFound());

app.MapPost("/api/v1/gps/points", (List<ApiGeoPointDto> points) =>
{
    store.InsertGpsPoints(points);
    return Results.NoContent();
});
app.MapGet("/api/v1/gps/nearest", (DateTime date) =>
{
    ApiGeoPointDto point = store.GetGpsNearest(date);
    return point == null ? Results.NotFound() : Results.Json(point);
});
app.MapGet("/api/v1/gps/files", () => Results.Json(store.GetGpsFiles()));
app.MapDelete("/api/v1/gps/files", (DateTime fileTime) =>
    Results.Json(store.DeleteGpsFile(fileTime)));
app.MapGet("/api/v1/gps", (DateTime? from, DateTime? to) =>
    Results.Json(store.GetGpsPoints(from ?? default, to ?? default)));

app.MapGet("/api/v1/meteorology", (DateTime? from, DateTime? to) =>
    Results.Json(store.GetMeteorology(from ?? default, to ?? default)));
app.MapPost("/api/v1/meteorology", (List<ApiMeteorologyDto> data) =>
{
    store.InsertMeteorology(data);
    return Results.NoContent();
});

app.MapGet("/api/v1/requests", () => Results.Json(store.GetRequests()));
app.MapPost("/api/v1/requests", (ApiUserRequestDto request) => Results.Json(store.SaveRequest(request)));
app.MapDelete("/api/v1/requests/{id:int}", (int id) =>
{
    store.DeleteRequest(id);
    return Results.NoContent();
});
app.MapGet("/api/v1/requests/{id:int}/sql", (int id) =>
    Results.Json(new ApiTextDto { Value = store.GetRequestSql(id) }));
app.MapPut("/api/v1/requests/{id:int}/sql", (int id, ApiTextDto sql) =>
{
    store.SetRequestSql(id, sql?.Value);
    return Results.NoContent();
});

app.MapPost("/api/v1/duplicates/check", (ApiMediaActionDto action) =>
    Results.Json(store.CheckDuplicate(action)));
app.MapGet("/api/v1/duplicates/{id:int}", (int id) => Results.Json(store.GetDuplicates(id)));

app.MapPost("/api/v1/maintenance/clean", () => Results.NoContent());

Console.WriteLine("ApiMock läuft auf " + urls);
Console.WriteLine("Frontend: MediaBrowserWPF.exe --provider api --api-base-url " + urls + " --api-key mock");
Console.WriteLine("Daten: " + store.Seed.MediaItems.Count + " Medien, " + store.Seed.Variations.Count + " Variationen, " +
                  store.Seed.Categories.Count + " Kategorien");

app.Run();

static string GetArg(string[] args, string name, string defaultValue)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                return args[i + 1];
            return defaultValue;
        }
    }

    return defaultValue;
}
