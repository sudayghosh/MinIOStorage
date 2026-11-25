using System.Net.Http.Json;

namespace FileManagement.Web.Services;

public class ApiFileStorageService : IFileStorageService
{
	private readonly HttpClient _http;
	private readonly ILogger<ApiFileStorageService> _logger;

	public ApiFileStorageService(HttpClient http, ILogger<ApiFileStorageService> logger)
	{
		_http = http;
		_logger = logger;
	}

	public async Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken ct = default)
	{
		var items = await _http.GetFromJsonAsync<List<FileItem>>("api/files", ct);
		return items ?? new List<FileItem>();
	}

	public async Task UploadAsync(string objectName, Stream content, long size, string contentType, CancellationToken ct = default)
	{
		using var form = new MultipartFormDataContent();
		var fileContent = new StreamContent(content);

		if (!string.IsNullOrWhiteSpace(contentType))
		{
			fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
		}

		form.Add(fileContent, "file", objectName);

		using var response = await _http.PostAsync("api/files/upload", form, ct);
		response.EnsureSuccessStatusCode();
	}

	public async Task DeleteAsync(string objectName, CancellationToken ct = default)
	{
		using var response = await _http.DeleteAsync($"api/files/{Uri.EscapeDataString(objectName)}", ct);
		response.EnsureSuccessStatusCode();
	}
}
