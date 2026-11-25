namespace FileManagement.Web.Services;

public interface IFileStorageService
{
	Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken ct = default);
	Task UploadAsync(string objectName, Stream content, long size, string contentType, CancellationToken ct = default);
	Task DeleteAsync(string objectName, CancellationToken ct = default);
}
