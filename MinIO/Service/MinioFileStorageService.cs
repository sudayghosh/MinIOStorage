using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using MinIOStorage.Common;

namespace MinIOStorage.Service
{
	public class MinioFileStorageService : IFileStorageService
	{
		private readonly IMinioClient _minio;
        private readonly MinioSettings _settings;

        public MinioFileStorageService(
            IMinioClient minio,
            IOptions<MinioSettings> settings)
        {
            _minio = minio;
            _settings = settings.Value;
        }

        public async Task<string> UploadAsync(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            var bucket = _settings.BucketName;

            bool exists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucket),
                cancellationToken);

            if (!exists)
            {
                await _minio.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(bucket),
                    cancellationToken);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

            using var stream = file.OpenReadStream();

            await _minio.PutObjectAsync(
                new PutObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(fileName)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType),
                cancellationToken);

            return fileName;
        }

        public async Task<string> GetFileUrlAsync(string fileName)
        {
            return await _minio.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(fileName)
                    .WithExpiry(60 * 10));
        }

        public async Task DeleteAsync(string fileName)
        {
            await _minio.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(fileName));
        }

        public async Task<IReadOnlyList<StoredFile>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            var bucket = _settings.BucketName;

            bool exists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucket),
                cancellationToken);

            if (!exists)
            {
                return Array.Empty<StoredFile>();
            }

            var results = new List<StoredFile>();
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucket)
                .WithRecursive(true);

            await foreach (var item in _minio.ListObjectsEnumAsync(listArgs, cancellationToken))
            {
                results.Add(new StoredFile(
                    item.Key,
                    (long)item.Size,
                    item.LastModifiedDateTime ?? DateTime.MinValue));
            }

            return results
                .OrderByDescending(f => f.LastModified)
                .ToList();
        }
	}
}
