using Dapper;
using Npgsql;
using System.Data;
using QuizAPI.Models;

public interface IQuestionRepository
{
    Task<int> AddQuestionAsync(Question q);
    Task<int> AddChoiceAsync(Choice c);
    Task UpdateQuestionAsync(UpdateQuestionRequest req);
    Task DeleteQuestionAsync(int id);
}

public class QuestionRepository : IQuestionRepository
{
    private readonly IConfiguration _config;

    public QuestionRepository(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Connection =>
        new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));

    // =========================
    // ADD QUESTION (FIXED)
    // =========================
    public async Task<int> AddQuestionAsync(Question q)
    {
        var sql = @"
            INSERT INTO Question (QuizId, Text)
            VALUES (@QuizId, @Text)
            RETURNING Id;
        ";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, q);
    }

    // =========================
    // ADD CHOICE (FIXED)
    // =========================
    public async Task<int> AddChoiceAsync(Choice c)
    {
        var sql = @"
            INSERT INTO Choice (QuestionId, Text, IsCorrect)
            VALUES (@QuestionId, @Text, @IsCorrect)
            RETURNING Id;
        ";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, c);
    }

    // =========================
    // UPDATE QUESTION
    // =========================
    public async Task UpdateQuestionAsync(UpdateQuestionRequest req)
    {
        var sql = @"
            UPDATE Question
            SET Text = @Text
            WHERE Id = @Id;
        ";

        using var db = Connection;
        await db.ExecuteAsync(sql, req);
    }

    // =========================
    // DELETE QUESTION
    // =========================
    public async Task DeleteQuestionAsync(int id)
    {
        using var db = Connection;

        // delete choices first (FK safe)
        await db.ExecuteAsync(@"
            DELETE FROM Choice
            WHERE QuestionId = @id;
        ", new { id });

        // delete question
        await db.ExecuteAsync(@"
            DELETE FROM Question
            WHERE Id = @id;
        ", new { id });
    }
}