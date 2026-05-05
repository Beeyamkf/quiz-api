using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using QuizAPI.Models;

public interface IStudentRepository
{
    Task<int> CreateAttempt(StudentAttempt attempt);
    Task SaveAnswer(StudentAnswer answer);
    Task<int> GetCorrectChoice(int questionId);
    Task<int> CalculateScore(int attemptId);
    Task UpdateScore(int attemptId, int score);

    Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId);
    Task<IEnumerable<QuizResultDto>> GetQuizResults(int quizId);
    Task<object> GetResultDetails(int resultId);
}
public class StudentRepository : IStudentRepository
{
    private readonly IConfiguration _config;
 

    public StudentRepository(IConfiguration config)
    {
        _config = config;

    }

    private IDbConnection Connection =>
        new SqlConnection(_config.GetConnectionString("DefaultConnection"));

    // =====================
    // CREATE ATTEMPT
    // =====================
    public async Task<int> CreateAttempt(StudentAttempt a)
    {
        var sql = @"
    INSERT INTO StudentAttempt (QuizId, FullName, Score, CreatedAt)
    VALUES (@QuizId, @FullName, 0, GETDATE());
    SELECT CAST(SCOPE_IDENTITY() as int);";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, new
        {
            a.QuizId,
            a.FullName
        });
    }

    // =====================
    // SAVE ANSWER
    // =====================
    public async Task SaveAnswer(StudentAnswer a)
    {
        var sql = @"
    IF NOT EXISTS (
        SELECT 1 FROM StudentAnswer
        WHERE AttemptId = @AttemptId AND QuestionId = @QuestionId
    )
    INSERT INTO StudentAnswer (AttemptId, QuestionId, ChoiceId, IsCorrect)
    VALUES (@AttemptId, @QuestionId, @ChoiceId, @IsCorrect);";

        using var db = Connection;
        await db.ExecuteAsync(sql, a);
    }
    // =====================
    // GET CORRECT ANSWER
    // =====================
    public async Task<int> GetCorrectChoice(int questionId)
    {
        var sql = @"
        SELECT Id 
        FROM Choice
        WHERE QuestionId = @questionId AND IsCorrect = 1";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<int>(sql, new { questionId });
    }

    // =====================
    // SCORE
    // =====================
    public async Task<int> CalculateScore(int attemptId)
    {
        var sql = @"
    SELECT ISNULL(COUNT(*),0)
    FROM StudentAnswer
    WHERE AttemptId = @attemptId AND IsCorrect = 1";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<int>(sql, new { attemptId });
    }

    public async Task UpdateScore(int attemptId, int score)
    {
        var sql = @"
        UPDATE StudentAttempt
        SET Score = @score
        WHERE Id = @attemptId";

        using var db = Connection;
        await db.ExecuteAsync(sql, new { attemptId, score });
    }

    // =====================
    // QUIZ FOR STUDENT (FIXED)
    // =====================
    public async Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId)
    {
        using var db = Connection;

        var quizId = await db.QueryFirstAsync<int>(@"
            SELECT QuizId 
            FROM StudentAttempt 
            WHERE Id = @attemptId
        ", new { attemptId });

        var title = await db.QueryFirstOrDefaultAsync<string>(@"
            SELECT Title 
            FROM Quiz 
            WHERE Id = @quizId
        ", new { quizId });

        var rows = await db.QueryAsync(@"
            SELECT 
                q.Id AS QuestionId,
                q.Text AS QuestionText,
                c.Id AS ChoiceId,
                c.Text AS ChoiceText
            FROM Question q
            LEFT JOIN Choice c ON q.Id = c.QuestionId
            WHERE q.QuizId = @quizId
        ", new { quizId });

        var dict = new Dictionary<int, StudentQuestionDto>();

        foreach (var r in rows)
        {
            int qId = r.QuestionId;

            if (!dict.ContainsKey(qId))
            {
                dict[qId] = new StudentQuestionDto
                {
                    Id = qId,
                    Text = r.QuestionText,
                    Choices = new List<StudentChoiceDto>()
                };
            }

            if (r.ChoiceId != null)
            {
                dict[qId].Choices.Add(new StudentChoiceDto
                {
                    Id = r.ChoiceId,
                    Text = r.ChoiceText
                });
            }
        }

        return new StudentQuizDto
        {
            Title = title ?? "",
            Questions = dict.Values.ToList()
        };
    }

    // =====================
    // TEACHER RESULTS
    // =====================
    public async Task<IEnumerable<QuizResultDto>> GetQuizResults(int quizId)
    {
        var sql = @"
    SELECT 
        Id,
        FullName,
        Score,
        CreatedAt
    FROM StudentAttempt
    WHERE QuizId = @quizId
    ORDER BY Id DESC";

        using var db = Connection;
        return await db.QueryAsync<QuizResultDto>(sql, new { quizId });
    }
    public async Task<object> GetResultDetails(int resultId)
    {
        using var db = Connection;

        // 1. Get attempt info
        var attempt = await db.QueryFirstOrDefaultAsync(@"
        SELECT Id, FullName, Score
        FROM StudentAttempt
        WHERE Id = @resultId
    ", new { resultId });

        if (attempt == null)
            return null;

        // 2. Get questions + choices + student answers
        var rows = await db.QueryAsync(@"
        SELECT 
            q.Id AS QuestionId,
            q.Text AS QuestionText,
            c.Id AS ChoiceId,
            c.Text AS ChoiceText,
            c.IsCorrect,
            sa.ChoiceId AS SelectedChoiceId
        FROM Question q
        LEFT JOIN Choice c ON q.Id = c.QuestionId
        LEFT JOIN StudentAnswer sa 
            ON sa.QuestionId = q.Id AND sa.AttemptId = @resultId
        WHERE q.QuizId = (
            SELECT QuizId FROM StudentAttempt WHERE Id = @resultId
        )
    ", new { resultId });

        var dict = new Dictionary<int, dynamic>();

        foreach (var r in rows)
        {
            int qId = r.QuestionId;

            if (!dict.ContainsKey(qId))
            {
                dict[qId] = new
                {
                    questionText = r.QuestionText,
                    choices = new List<object>()
                };
            }

            dict[qId].choices.Add(new
            {
                text = r.ChoiceText,
                isCorrect = r.IsCorrect,
                isSelected = r.SelectedChoiceId == r.ChoiceId
            });
        }

        return new
        {
            fullName = attempt.FullName,
            score = attempt.Score,
            total = dict.Count,
            answers = dict.Values
        };
    }
}