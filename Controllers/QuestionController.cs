using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;
using QuizAPI.Repositories;

[ApiController]
[Route("api/question")]
[Authorize]
public class QuestionController : ControllerBase
{
    private readonly IQuestionRepository _repo;

    public QuestionController(IQuestionRepository repo)
    {
        _repo = repo;
    }

    [HttpPost("create-full")]
    public async Task<IActionResult> CreateFull([FromBody] CreateQuestionWithChoicesRequest req)
    {
        if (req == null)
            return BadRequest("Request is null");

        if (string.IsNullOrWhiteSpace(req.Text))
            return BadRequest("Question text is required");

        if (req.Choices == null || req.Choices.Count < 2)
            return BadRequest("At least 2 choices required");

        var questionId = await _repo.AddQuestionAsync(new Question
        {
            QuizId = req.QuizId,
            Text = req.Text
        });

        foreach (var c in req.Choices)
        {
            await _repo.AddChoiceAsync(new Choice
            {
                QuestionId = questionId,
                Text = c.Text,
                IsCorrect = c.IsCorrect
            });
        }

        return Ok(new
        {
            questionId
        });
    }


    // =========================
    // UPDATE QUESTION TEXT
    // =========================
    [HttpPut("update")]
    public async Task<IActionResult> Update(UpdateQuestionRequest req)
    {
        await _repo.UpdateQuestionAsync(req);
        return Ok("Question updated");
    }

    // =========================
    // DELETE QUESTION
    // =========================
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _repo.DeleteQuestionAsync(id);
        return Ok("Question deleted");
    }
}