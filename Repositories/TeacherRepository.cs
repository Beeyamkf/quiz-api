using Dapper;
using Microsoft.Data.SqlClient;
using Npgsql;
using QuizAPI.Models;
using System.Data;

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
        VALUES (@FullName, @Email, @PasswordHash)";

        using var db = Connection;
        return await db.ExecuteAsync(sql, teacher);
    }

    public async Task<Teacher> GetByEmailAsync(string email)
    {
        var sql = "SELECT * FROM Teacher WHERE Email = @Email";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<Teacher>(sql, new { Email = email });
    }
}