using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using QuizAPI.Models;
using System;
using System.Security.Claims;

[ApiController]
[Route("api/quiz")]
public class QuizController : ControllerBase
{
    private readonly IQuizRepository _repo;

    public QuizController(IQuizRepository repo)
    {
        _repo = repo;
    }

    // =====================
    // CREATE QUIZ (FIXED + SAFE JWT)
    // =====================
    [Authorize]
    [HttpPost("create")]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizRequest req)
    {
        try
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Title))
                return BadRequest("Title is required");

            // 🔥 SAFE JWT CLAIM EXTRACTION
            var teacherIdClaim =
                User.FindFirst("TeacherId")?.Value ??
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(teacherIdClaim))
                return Unauthorized("TeacherId missing in token");

            if (!int.TryParse(teacherIdClaim, out int teacherId))
                return Unauthorized("Invalid TeacherId in token");

            var code = "QZ" + Guid.NewGuid().ToString("N")[..6].ToUpper();

            var quiz = new Quiz
            {
                Title = req.Title,
                TeacherId = teacherId,
                Code = code
            };

            var id = await _repo.CreateQuizAsync(quiz);

            var qr = GenerateQr(code);

            return Ok(new
            {
                quizId = id,
                code,
                qrBase64 = qr
            });
        }
        catch (Exception ex)
        {
            // 🔥 IMPORTANT: avoid silent 500
            return StatusCode(500, new
            {
                message = "Server error in CreateQuiz",
                error = ex.Message
            });
        }
    }

    // =====================
    // GET QUIZ FULL
    // =====================
    [HttpGet("{quizId}/full")]
    public async Task<IActionResult> GetQuizFull(int quizId)
    {
        var data = await _repo.GetQuizFull(quizId);
        return Ok(data);
    }

    // =====================
    // MY QUIZZES
    // =====================
    [HttpGet("my-quizzes/{teacherId}")]
    public async Task<IActionResult> MyQuizzes(int teacherId)
    {
        var data = await _repo.GetByTeacherIdAsync(teacherId);
        return Ok(data);
    }

    // =====================
    // DELETE QUIZ
    // =====================
    [HttpDelete("{quizId}")]
    public async Task<IActionResult> DeleteQuiz(int quizId)
    {
        await _repo.DeleteQuizAsync(quizId);
        return Ok(new { message = "Quiz deleted" });
    }

    // =====================
    // GET QUIZ WITH QR
    // =====================
    [HttpGet("{quizId}/with-qr")]
    public async Task<IActionResult> GetQuizWithQr(int quizId)
    {
        var quiz = await _repo.GetByIdAsync(quizId);

        if (quiz == null)
            return NotFound(new { message = "Quiz not found" });

        var qr = GenerateQr(quiz.Code);
        var data = await _repo.GetQuizFull(quizId);

        return Ok(new
        {
            quizId,
            code = quiz.Code,
            qrBase64 = qr,
            questions = data
        });
    }

    // =====================
    // CLEAR RESULTS
    // =====================
    [HttpDelete("{quizId}/clear-results")]
    public async Task<IActionResult> ClearResults(int quizId)
    {
        await _repo.ClearQuizResultsAsync(quizId);
        return Ok(new { message = "Results cleared" });
    }

    // =====================
    // UPDATE QUIZ
    // =====================
    [HttpPut("{quizId}")]
    public async Task<IActionResult> UpdateQuiz(int quizId, [FromBody] UpdateQuizRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Title is required");

        await _repo.UpdateQuizAsync(quizId, req.Title);
        return Ok(new { message = "Quiz updated" });
    }

    // =====================
    // QR GENERATOR
    // =====================
    private string GenerateQr(string text)
    {
        using var qr = new QRCodeGenerator();
        var data = qr.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);

        return Convert.ToBase64String(png.GetGraphic(20));
    }
}