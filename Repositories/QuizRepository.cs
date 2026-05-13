using Dapper;
using Npgsql;
using QuizAPI.Models;
using System.Data;

public class QuizRepository : IQuizRepository
{
    private readonly IConfiguration _config;

    public QuizRepository(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Connection =>
        new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));

    // =====================
    // CREATE QUIZ (FIXED)
    // =====================
    public async Task<int> CreateQuizAsync(Quiz quiz)
    {
        var sql = @"
            INSERT INTO Quiz (Title, Code, TeacherId)
            VALUES (@Title, @Code, @TeacherId)
            RETURNING Id;
        ";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, quiz);
    }

    // =====================
    // GET BY CODE
    // =====================
    public async Task<Quiz> GetByCode(string code)
    {
        var sql = @"
            SELECT * 
            FROM Quiz 
            WHERE Code = @code
            LIMIT 1;
        ";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { code });
    }

    // =====================
    // GET BY ID
    // =====================
    public async Task<Quiz> GetByIdAsync(int id)
    {
        var sql = "SELECT * FROM Quiz WHERE Id = @Id";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { Id = id });
    }

    // =====================
    // BY TEACHER
    // =====================
    public async Task<IEnumerable<Quiz>> GetByTeacherIdAsync(int teacherId)
    {
        var sql = @"
            SELECT Id, Title, Code, TeacherId
            FROM Quiz
            WHERE TeacherId = @teacherId
            ORDER BY Id DESC;
        ";

        using var db = Connection;
        return await db.QueryAsync<Quiz>(sql, new { teacherId });
    }

    // =====================
    // GET FULL QUIZ (SIMPLIFIED SAFE)
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
    // DELETE QUIZ
    // =====================
    public async Task DeleteQuizAsync(int quizId)
    {
        using var db = Connection;

        await db.ExecuteAsync("DELETE FROM StudentAnswer WHERE AttemptId IN (SELECT Id FROM StudentAttempt WHERE QuizId=@quizId)", new { quizId });
        await db.ExecuteAsync("DELETE FROM StudentAttempt WHERE QuizId=@quizId", new { quizId });
        await db.ExecuteAsync("DELETE FROM Choice WHERE QuestionId IN (SELECT Id FROM Question WHERE QuizId=@quizId)", new { quizId });
        await db.ExecuteAsync("DELETE FROM Question WHERE QuizId=@quizId", new { quizId });
        await db.ExecuteAsync("DELETE FROM Quiz WHERE Id=@quizId", new { quizId });
    }

    // =====================
    // CLEAR RESULTS
    // =====================
    public async Task ClearQuizResultsAsync(int quizId)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"
            DELETE FROM StudentAnswer
            WHERE AttemptId IN (SELECT Id FROM StudentAttempt WHERE QuizId=@quizId);
        ", new { quizId });

        await db.ExecuteAsync(@"
            DELETE FROM StudentAttempt
            WHERE QuizId=@quizId;
        ", new { quizId });
    }

    // =====================
    // RESULTS LIST
    // =====================
    public async Task<IEnumerable<object>> GetQuizResults(int quizId)
    {
        var sql = @"
            SELECT Id, FullName, Score, CreatedAt
            FROM StudentAttempt
            WHERE QuizId = @quizId
            ORDER BY Id DESC;
        ";

        using var db = Connection;
        return await db.QueryAsync(sql, new { quizId });
    }

    // =====================
    // UPDATE QUIZ
    // =====================
    public async Task UpdateQuizAsync(int quizId, string title)
    {
        var sql = @"
            UPDATE Quiz
            SET Title = @title
            WHERE Id = @quizId;
        ";

        using var db = Connection;
        await db.ExecuteAsync(sql, new { quizId, title });
    }
}