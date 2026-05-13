using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using QuizAPI.Models;
using System;
using System.Data;


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
    public async Task<Quiz> GetByCode(string code)
        {
            var sql = "SELECT * FROM Quiz WHERE Code = @code";

            using var db = Connection;
            return await db.QueryFirstOrDefaultAsync<Quiz>(sql, new { code });
        }

    public async Task<IEnumerable<QuestionWithChoicesDto>> GetQuizFull(int quizId)
    {
        var sql = @"
    SELECT 
        q.Id, q.Text,
        c.Id, c.Text
    FROM Question q
    LEFT JOIN Choice c ON q.Id = c.QuestionId
    WHERE q.QuizId = @quizId";

        using var db = Connection;

        var questionDict = new Dictionary<int, QuestionWithChoicesDto>();

        var result = await db.QueryAsync<QuestionWithChoicesDto, ChoiceDto, QuestionWithChoicesDto>(
            sql,
            (q, c) =>
            {
                if (!questionDict.TryGetValue(q.Id, out var question))
                {
                    question = q;
                    question.Choices = new List<ChoiceDto>();
                    questionDict.Add(q.Id, question);
                }

                if (c != null)
                    question.Choices.Add(c);

                return question;
            },
            new { quizId },
            splitOn: "Id"
        );

        return questionDict.Values;
    }
    public async Task<int> CreateQuizAsync(Quiz quiz)
        {
            var sql = @"
        INSERT INTO Quiz (Title, Code, TeacherId)
        VALUES (@Title, @Code, @TeacherId);
        SELECT CAST(SCOPE_IDENTITY() as int);";

            using var db = Connection;
            return await db.QuerySingleAsync<int>(sql, quiz);
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
    public async Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId)
    {
        using var db = Connection;

        // 1. GET QUIZ ID FROM ATTEMPT
        var quizId = await db.QueryFirstOrDefaultAsync<int>(@"
        SELECT QuizId FROM Attempt WHERE Id = @attemptId
    ", new { attemptId });

        // 2. GET QUIZ TITLE
        var title = await db.QueryFirstOrDefaultAsync<string>(@"
        SELECT Title FROM Quiz WHERE Id = @quizId
    ", new { quizId });

        // 3. GET QUESTIONS + CHOICES
        var sql = @"
        SELECT 
            q.Id, q.Text,
            c.Id, c.Text
        FROM Question q
        LEFT JOIN Choice c ON q.Id = c.QuestionId
        WHERE q.QuizId = @quizId
    ";

        var questionDict = new Dictionary<int, StudentQuestionDto>();

        var result = await db.QueryAsync<StudentQuestionDto, StudentChoiceDto, StudentQuestionDto>(
            sql,
            (q, c) =>
            {
                if (!questionDict.TryGetValue(q.Id, out var question))
                {
                    question = new StudentQuestionDto
                    {
                        Id = q.Id,
                        Text = q.Text,
                        Choices = new List<StudentChoiceDto>()
                    };

                    questionDict.Add(q.Id, question);
                }

                if (c != null)
                {
                    question.Choices.Add(new StudentChoiceDto
                    {
                        Id = c.Id,
                        Text = c.Text
                    });
                }

                return question;
            },
            new { quizId },
            splitOn: "Id"
        );

        return new StudentQuizDto
        {
            Title = title,
            Questions = questionDict.Values.ToList()
        };
    }
    public async Task DeleteQuizAsync(int quizId)
    {
        using var db = Connection;

        // 1. delete answers
        await db.ExecuteAsync(@"
        DELETE FROM StudentAnswer
        WHERE AttemptId IN (SELECT Id FROM StudentAttempt WHERE QuizId = @quizId)
    ", new { quizId });

        // 2. delete attempts
        await db.ExecuteAsync(@"
        DELETE FROM StudentAttempt
        WHERE QuizId = @quizId
    ", new { quizId });

        // 3. delete choices
        await db.ExecuteAsync(@"
        DELETE FROM Choice
        WHERE QuestionId IN (SELECT Id FROM Question WHERE QuizId = @quizId)
    ", new { quizId });

        // 4. delete questions
        await db.ExecuteAsync(@"
        DELETE FROM Question
        WHERE QuizId = @quizId
    ", new { quizId });

        // 5. delete quiz
        await db.ExecuteAsync(@"
        DELETE FROM Quiz
        WHERE Id = @quizId
    ", new { quizId });
    }
    public async Task ClearQuizResultsAsync(int quizId)
    {
        using var db = Connection;

        await db.ExecuteAsync(@"
        DELETE FROM StudentAnswer
        WHERE AttemptId IN (SELECT Id FROM StudentAttempt WHERE QuizId = @quizId)
    ", new { quizId });

        await db.ExecuteAsync(@"
        DELETE FROM StudentAttempt
        WHERE QuizId = @quizId
    ", new { quizId });
    }
    public async Task<IEnumerable<object>> GetQuizResults(int quizId)
    {
        using var db = Connection;

        var sql = @"
        SELECT 
            ta.Id,
            ta.FullName,
            ta.Score,
            ta.CreatedAt
        FROM StudentAttempt ta
        WHERE ta.QuizId = @quizId
        ORDER BY ta.Id DESC";

        return await db.QueryAsync(sql, new { quizId });
    }
    public async Task UpdateQuizAsync(int quizId, string title)
    {
        using var db = Connection;

        var sql = @"
    UPDATE Quiz
    SET Title = @title
    WHERE Id = @quizId";

        await db.ExecuteAsync(sql, new { quizId, title });
    }
}
