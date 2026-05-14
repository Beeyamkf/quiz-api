using Dapper;
using Npgsql;
using System.Data;
using QuizAPI.Models;

public interface IQuizRepository
{
    Task<int> CreateQuizAsync(Quiz quiz);
    Task<Quiz> GetByIdAsync(int id);
    Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId);
    Task<Quiz> GetByCode(string code);
    Task<IEnumerable<QuestionWithChoicesDto>> GetQuizFull(int quizId);
    Task<IEnumerable<Quiz>> GetByTeacherIdAsync(int teacherId);
    Task DeleteQuizAsync(int quizId);
    Task ClearQuizResultsAsync(int quizId);
    Task<IEnumerable<object>> GetQuizResults(int quizId);
    Task UpdateQuizAsync(int quizId, string title);
}

public class QuizRepository : IQuizRepository
{
    private readonly IConfiguration _config;

    public QuizRepository(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Connection =>
        new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));

    // =========================
    // CREATE QUIZ (FIXED)
    // =========================
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

    public async Task<Quiz> GetByCode(string code)
    {
        var sql = "SELECT * FROM Quiz WHERE Code = @code";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { code });
    }

    public async Task<Quiz> GetByIdAsync(int id)
    {
        var sql = "SELECT * FROM Quiz WHERE Id = @Id";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Quiz>> GetByTeacherIdAsync(int teacherId)
    {
        var sql = @"
            SELECT Id, Title, Code, TeacherId
            FROM Quiz
            WHERE TeacherId = @teacherId
            ORDER BY Id DESC";

        using var db = Connection;
        return await db.QueryAsync<Quiz>(sql, new { teacherId });
    }

    // =========================
    // GET FULL QUIZ
    // =========================
    public async Task<IEnumerable<QuestionWithChoicesDto>> GetQuizFull(int quizId)
    {
        var sql = @"
    SELECT 
        q.Id,
        q.Text,
        c.Id,
        c.Text,
        c.IsCorrect
    FROM Question q
    LEFT JOIN Choice c ON q.Id = c.QuestionId
    WHERE q.QuizId = @quizId";

        using var db = Connection;

        var dict = new Dictionary<int, QuestionWithChoicesDto>();

        await db.QueryAsync<QuestionWithChoicesDto, ChoiceDto, QuestionWithChoicesDto>(
        sql,
        (q, c) =>
        {
            if (!dict.TryGetValue(q.Id, out var question))
            {
                question = new QuestionWithChoicesDto
                {
                    Id = q.Id,
                    Text = q.Text,
                    Choices = new List<ChoiceDto>()
                };

                dict.Add(q.Id, question);
            }

            if (c != null && c.Id != 0)
            {
                question.Choices.Add(c);
            }

            return question;
        },
        new { quizId },
        splitOn: "Id"
    );

        return dict.Values;
    }

    // =========================
    // FIXED: GET QUIZ BY ATTEMPT
    // =========================
    public async Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId)
    {
        using var db = Connection;

        var quizId = await db.QueryFirstAsync<int>(@"
            SELECT QuizId FROM StudentAttempt WHERE Id = @attemptId
        ", new { attemptId });

        var title = await db.QueryFirstOrDefaultAsync<string>(@"
            SELECT Title FROM Quiz WHERE Id = @quizId
        ", new { quizId });

        var sql = @"
            SELECT q.Id, q.Text,
                   c.Id, c.Text
            FROM Question q
            LEFT JOIN Choice c ON q.Id = c.QuestionId
            WHERE q.QuizId = @quizId";

        var dict = new Dictionary<int, StudentQuestionDto>();

        await db.QueryAsync<StudentQuestionDto, StudentChoiceDto, StudentQuestionDto>(
            sql,
            (q, c) =>
            {
                if (!dict.ContainsKey(q.Id))
                {
                    dict[q.Id] = new StudentQuestionDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Choices = new List<StudentChoiceDto>()
                    };
                }

                if (c != null)
                {
                    dict[q.Id].Choices.Add(c);
                }

                return dict[q.Id];
            },
            new { quizId },
            splitOn: "Id"
        );

        return new StudentQuizDto
        {
            Title = title ?? "",
            Questions = dict.Values.ToList()
        };
    }

    // =========================
    // DELETE QUIZ
    // =========================
    public async Task DeleteQuizAsync(int quizId)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"DELETE FROM StudentAnswer WHERE AttemptId IN (SELECT Id FROM StudentAttempt WHERE QuizId=@quizId)", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM StudentAttempt WHERE QuizId=@quizId", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM Choice WHERE QuestionId IN (SELECT Id FROM Question WHERE QuizId=@quizId)", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM Question WHERE QuizId=@quizId", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM Quiz WHERE Id=@quizId", new { quizId });
    }

    public async Task ClearQuizResultsAsync(int quizId)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"DELETE FROM StudentAnswer WHERE AttemptId IN (SELECT Id FROM StudentAttempt WHERE QuizId=@quizId)", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM StudentAttempt WHERE QuizId=@quizId", new { quizId });
    }

    public async Task<IEnumerable<object>> GetQuizResults(int quizId)
    {
        using var db = Connection;

        var sql = @"
            SELECT Id, FullName, Score, CreatedAt
            FROM StudentAttempt
            WHERE QuizId = @quizId
            ORDER BY Id DESC";

        return await db.QueryAsync(sql, new { quizId });
    }

    public async Task UpdateQuizAsync(int quizId, string title)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"
            UPDATE Quiz
            SET Title = @title
            WHERE Id = @quizId
        ", new { quizId, title });
    }
}