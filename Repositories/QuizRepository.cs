using Dapper;
using Npgsql;
using System.Data;
using QuizAPI.Models;

public interface IQuizRepository
{
    Task<int> CreateQuizAsync(Quiz quiz);
    Task<Quiz?> GetByIdAsync(int id);
    Task<Quiz?> GetByCode(string code);
    Task<IEnumerable<Quiz>> GetByTeacherIdAsync(int teacherId);

    Task<IEnumerable<QuestionWithChoicesDto>> GetQuizFull(int quizId);
    Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId);

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
    // CREATE QUIZ
    // =========================
    public async Task<int> CreateQuizAsync(Quiz quiz)
    {
        var sql = @"
            INSERT INTO quiz (title, code, teacherid)
            VALUES (@title, @code, @teacherId)
            RETURNING id;
        ";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, quiz);
    }

    // =========================
    // GET BY ID
    // =========================
    public async Task<Quiz?> GetByIdAsync(int id)
    {
        var sql = "SELECT id, title, code, teacherid FROM quiz WHERE id = @id";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { id });
    }

    // =========================
    // GET BY CODE
    // =========================
    public async Task<Quiz?> GetByCode(string code)
    {
        var sql = "SELECT id, title, code, teacherid FROM quiz WHERE code = @code";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { code });
    }

    // =========================
    // GET BY TEACHER
    // =========================
    public async Task<IEnumerable<Quiz>> GetByTeacherIdAsync(int teacherId)
    {
        var sql = @"
            SELECT id, title, code, teacherid
            FROM quiz
            WHERE teacherid = @teacherId
            ORDER BY id DESC;
        ";

        using var db = Connection;
        return await db.QueryAsync<Quiz>(sql, new { teacherId });
    }

    // =========================
    // GET FULL QUIZ (FIXED DAPPER MAPPING)
    // =========================
    public async Task<IEnumerable<QuestionWithChoicesDto>> GetQuizFull(int quizId)
    {
        var sql = @"
SELECT 
    q.id,
    q.text,
    c.id,
    c.text,
    c.iscorrect
FROM question q
LEFT JOIN choice c ON q.id = c.questionid
WHERE q.quizid = @quizId
ORDER BY q.id;
";

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
            splitOn: "id"
        );

        return dict.Values;
    }

    // =========================
    // GET QUIZ BY ATTEMPT
    // =========================
    public async Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId)
    {
        using var db = Connection;

        var quizId = await db.QueryFirstAsync<int>(
            "SELECT quizid FROM studentattempt WHERE id = @attemptId",
            new { attemptId }
        );

        var title = await db.QueryFirstOrDefaultAsync<string>(
            "SELECT title FROM quiz WHERE id = @quizId",
            new { quizId }
        );

        var sql = @"
            SELECT 
                q.id,
                q.text,
                c.id ,
                c.text
            FROM question q
            LEFT JOIN choice c ON q.id = c.questionid
            WHERE q.quizid = @quizId;
        ";

        var dict = new Dictionary<int, StudentQuestionDto>();

        await db.QueryAsync<StudentQuestionDto, StudentChoiceDto, StudentQuestionDto>(
            sql,
            (q, c) =>
            {
                if (!dict.TryGetValue(q.Id, out var question))
                {
                    question = new StudentQuestionDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Choices = new List<StudentChoiceDto>()
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

        await db.ExecuteAsync(@"DELETE FROM studentanswer WHERE attemptid IN (SELECT id FROM studentattempt WHERE quizid=@quizId)", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM studentattempt WHERE quizid=@quizId", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM choice WHERE questionid IN (SELECT id FROM question WHERE quizid=@quizId)", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM question WHERE quizid=@quizId", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM quiz WHERE id=@quizId", new { quizId });
    }

    // =========================
    // CLEAR RESULTS
    // =========================
    public async Task ClearQuizResultsAsync(int quizId)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"DELETE FROM studentanswer WHERE attemptid IN (SELECT id FROM studentattempt WHERE quizid=@quizId)", new { quizId });
        await db.ExecuteAsync(@"DELETE FROM studentattempt WHERE quizid=@quizId", new { quizId });
    }

    // =========================
    // RESULTS
    // =========================
    public async Task<IEnumerable<object>> GetQuizResults(int quizId)
    {
        using var db = Connection;

        var sql = @"
            SELECT id, fullname, score, createdat
            FROM studentattempt
            WHERE quizid = @quizId
            ORDER BY id DESC;
        ";

        return await db.QueryAsync(sql, new { quizId });
    }

    // =========================
    // UPDATE QUIZ
    // =========================
    public async Task UpdateQuizAsync(int quizId, string title)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"
            UPDATE quiz
            SET title = @title
            WHERE id = @quizId
        ", new { quizId, title });
    }
}