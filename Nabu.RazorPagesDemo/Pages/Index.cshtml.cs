using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Nabu.RCL;

namespace Nabu.RazorPagesDemo.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IWhisperHandler _whisperHandler;

    public IndexModel(ILogger<IndexModel> logger, IWhisperHandler whisperHandler)
    {
        _logger = logger;
        _whisperHandler = whisperHandler;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostForwardToAgent([FromBody] TranscriptionRequest? request)
    {
        if (request?.Text is null)
        {
            return BadRequest();
        }

        await _whisperHandler.OnTranscriptionReadyAsync(request.Text);

        return new JsonResult(new { success = true, text = request.Text });
    }

    public class TranscriptionRequest
    {
        public string Text { get; set; } = "";
    }
}