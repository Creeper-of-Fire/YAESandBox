using Microsoft.AspNetCore.Mvc;

namespace YAESandBox.Plugin.Nodejs.CompilerHost;

// DTO for the request body
public class CompileRequestDto
{
    public string SourceCode { get; set; }
    public string FileType { get; set; } // e.g., "vue", "jsx"
}

[ApiController]
[Route("api/compiler")]
public class CompilerController(CompilerService compilerService) : ControllerBase
{
    private readonly CompilerService _compilerService = compilerService;

    [HttpPost("compile")]
    public async Task<IActionResult> Compile([FromBody] CompileRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            return BadRequest("SourceCode cannot be empty.");
        }

        try
        {
            var fileExtension = $".{request.FileType?.TrimStart('.') ?? "vue"}";
            var compiledCode = await _compilerService.CompileAsync(request.SourceCode, fileExtension);
            return Ok(new { CompiledCode = compiledCode });
        }
        catch (Exception ex)
        {
            // 返回一个包含详细错误信息的 500 错误，方便前端调试
            return StatusCode(500, $"An error occurred during compilation: {ex.Message}");
        }
    }
}