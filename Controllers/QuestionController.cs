using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;

[ApiController]
[Route("api/question")]
public class QuestionController : ControllerBase
{
    private readonly IQuestionRepository _repo;

    public QuestionController(IQuestionRepository repo)
    {
        _repo = repo;
    }

    // =========================
    // CREATE QUESTION + CHOICES
    // =========================
    [HttpPost("create-full")]
    public async Task<IActionResult> CreateFull(CreateQuestionWithChoicesRequest req)
    {
        var question = new Question
        {
            QuizId = req.QuizId,
            Text = req.Text
        };

        var questionId = await _repo.AddQuestionAsync(question);

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
            QuestionId = questionId,
            Message = "Created successfully"
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