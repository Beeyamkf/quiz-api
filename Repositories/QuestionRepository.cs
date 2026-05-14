using Dapper;
using Npgsql;
using System.Data;
using QuizAPI.Models;

namespace QuizAPI.Repositories;

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

        public async Task UpdateQuestionAsync(UpdateQuestionRequest req)
        {
            var sql = @"UPDATE Question SET Text=@Text WHERE Id=@Id";

            using var db = Connection;
            await db.ExecuteAsync(sql, req);
        }

    public async Task DeleteQuestionAsync(int id)
    {
        using var db = Connection;

        await db.ExecuteAsync(
            "DELETE FROM Choice WHERE QuestionId = @id",
            new { id }
        );

        await db.ExecuteAsync(
            "DELETE FROM Question WHERE Id = @id",
            new { id }
        );
    }
}
