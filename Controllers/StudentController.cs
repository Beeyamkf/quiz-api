using Microsoft.AspNetCore.Mvc;
using QuizAPI.Models;

[ApiController]
[Route("api/student")]
public class StudentController : ControllerBase
{
    private readonly IStudentRepository _repo;
    private readonly IQuizRepository _quizRepo;

    public StudentController(IStudentRepository repo, IQuizRepository quizRepo)
    {
        _repo = repo;
        _quizRepo = quizRepo;
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinQuiz(JoinQuizRequest req)
    {
        var quiz = await _quizRepo.GetByCode(req.QuizCode);

        if (quiz == null)
            return NotFound("Invalid QR Code");

        var attemptId = await _repo.CreateAttempt(new StudentAttempt
        {
            QuizId = quiz.Id,
            FullName = req.FullName
        });

        return Ok(new { attemptId, quizId = quiz.Id });
    }

    [HttpGet("quiz/{attemptId}")]
    public async Task<IActionResult> GetQuiz(int attemptId)
    {
        var quiz = await _repo.GetQuizByAttemptIdAsync(attemptId);
        return Ok(quiz);
    }

    [HttpPost("answer")]
    public async Task<IActionResult> Answer(SubmitAnswerRequest req)
    {
        var correct = await _repo.GetCorrectChoice(req.QuestionId);

        bool isCorrect = correct != 0 && correct == req.ChoiceId;


        await _repo.SaveAnswer(new StudentAnswer
        {
            AttemptId = req.AttemptId,
            QuestionId = req.QuestionId,
            ChoiceId = req.ChoiceId,
            IsCorrect = isCorrect
        });

        return Ok(new { isCorrect });
    }

    [HttpPost("finish/{attemptId}")]
    public async Task<IActionResult> Finish(int attemptId)
    {
        var score = await _repo.CalculateScore(attemptId);
        await _repo.UpdateScore(attemptId, score);

        return Ok(new { attemptId, score });
    }

    [HttpGet("result/{attemptId}")]
    public async Task<IActionResult> Result(int attemptId)
    {
        var score = await _repo.CalculateScore(attemptId);

        var quiz = await _repo.GetQuizByAttemptIdAsync(attemptId);

        var total = quiz.Questions.Count;

        return Ok(new
        {
            attemptId,
            score,
            total
        });
    }
}