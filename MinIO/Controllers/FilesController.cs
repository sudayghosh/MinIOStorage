using Microsoft.AspNetCore.Mvc;
using MinIOStorage.Common;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storage;

    public FilesController(IFileStorageService storage)
    {
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var files = await _storage.ListAsync(cancellationToken);
        return Ok(files);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Please select a file.");

        var fileName = await _storage.UploadAsync(file);

        return Ok(new
        {
            FileName = fileName
        });
    }

    [HttpGet("{fileName}")]
    public async Task<IActionResult> Get(string fileName)
    {
        var url = await _storage.GetFileUrlAsync(fileName);

        return Ok(new { Url = url });
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> Delete(string fileName)
    {
        await _storage.DeleteAsync(fileName);

        return Ok("File deleted.");
    }
}