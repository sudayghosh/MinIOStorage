namespace MinIOStorage.Common
{
    public interface IFileStorageService
    {
        Task<string> UploadAsync(
            IFormFile file,
            CancellationToken cancellationToken = default);

        Task<string> GetFileUrlAsync(string fileName);

        Task DeleteAsync(string fileName);

        Task<IReadOnlyList<StoredFile>> ListAsync(
            CancellationToken cancellationToken = default);
    }

    public record StoredFile(string Name, long Size, DateTime LastModified);
}
