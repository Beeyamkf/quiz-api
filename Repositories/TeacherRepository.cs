using Dapper;
using Npgsql;
using System.Data;
using QuizAPI.Models;

namespace QuizAPI.Repositories;

public interface ITeacherRepository
{
    Task<int> RegisterAsync(Teacher teacher);
    Task<Teacher> GetByEmailAsync(string email);
}

public class TeacherRepository : ITeacherRepository
{
    private readonly IConfiguration _config;

    public TeacherRepository(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Connection =>
        new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));

    public async Task<int> RegisterAsync(Teacher teacher)
    {
        var sql = @"
            INSERT INTO Teacher (FullName, Email, PasswordHash)
            VALUES (@FullName, @Email, @PasswordHash)
            RETURNING Id;
        ";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, teacher);
    }

    public async Task<Teacher> GetByEmailAsync(string email)
    {
        var sql = @"
            SELECT Id, FullName, Email, PasswordHash
            FROM Teacher
            WHERE Email = @Email
            LIMIT 1;
        ";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Teacher>(sql, new { Email = email });
    }
}