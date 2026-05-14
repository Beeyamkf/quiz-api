using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/teacher")]
[Authorize]
public class TeacherResultsController : ControllerBase
{
    private readonly IStudentRepository _repo;

    public TeacherResultsController(IStudentRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("quiz/{quizId}/results")]
    public async Task<IActionResult> GetResults(int quizId)
    {
        var data = await _repo.GetQuizResults(quizId);
        return Ok(data);
    }
    [HttpGet("result-details/{resultId}")]
    public async Task<IActionResult> GetResultDetails(int resultId)
    {
        var data = await _repo.GetResultDetails(resultId);

        if (data == null)
            return NotFound("Result not found");

        return Ok(data);
    }
}


