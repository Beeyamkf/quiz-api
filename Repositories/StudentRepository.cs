using Dapper;
using Npgsql;
using QuizAPI.Models;
using System.Data;

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
        new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));

    // =====================
    // CREATE ATTEMPT (FIXED)
    // =====================
    public async Task<int> CreateAttempt(StudentAttempt a)
    {
        var sql = @"
            INSERT INTO studentattempt (quizid, fullname, score, createdat)
            VALUES (@quizid, @fullname, 0, NOW())
            RETURNING id;
        ";

        using var db = Connection;
        return await db.QuerySingleAsync<int>(sql, new
        {
            a.QuizId,
            a.FullName
        });
    }

    // =====================
    // SAVE ANSWER (FIXED)
    // =====================
    public async Task SaveAnswer(StudentAnswer a)
    {
        var sql = @"
         INSERT INTO studentanswer (attemptid, questionid, choiceid, iscorrect)
SELECT @AttemptId, @QuestionId, @ChoiceId, @IsCorrect
WHERE NOT EXISTS (
    SELECT 1 FROM studentanswer
    WHERE attemptid = @AttemptId AND questionid = @QuestionId
);
        ";

        using var db = Connection;
        await db.ExecuteAsync(sql, a);
    }

    // =====================
    // GET CORRECT ANSWER
    // =====================
    public async Task<int> GetCorrectChoice(int questionId)
    {
        var sql = @"
            SELECT id 
            FROM choice
            WHERE questionid = @questionid AND iscorrect = true
            LIMIT 1;
        ";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<int>(sql, new { questionId });
    }

    // =====================
    // SCORE
    // =====================
    public async Task<int> CalculateScore(int attemptId)
    {
        var sql = @"
            SELECT COUNT(*)
            FROM studentanswer
            WHERE attemptid = @attemptId AND iscorrect = true;
        ";

        using var db = Connection;
        return await db.QueryFirstOrDefaultAsync<int>(sql, new { attemptId });
    }

    public async Task UpdateScore(int attemptId, int score)
    {
        var sql = @"
            UPDATE studentattempt
            SET score = @score
            WHERE id = @attemptId;
        ";

        using var db = Connection;
        await db.ExecuteAsync(sql, new { attemptId, score });
    }

    // =====================
    // QUIZ FOR STUDENT
    // =====================
    public async Task<StudentQuizDto> GetQuizByAttemptIdAsync(int attemptId)
    {
        using var db = Connection;

        var quizId = await db.QueryFirstAsync<int>(@"
        SELECT quizid 
        FROM studentattempt 
        WHERE id = @attemptId
    ", new { attemptId });

        var title = await db.QueryFirstOrDefaultAsync<string>(@"
        SELECT title 
        FROM quiz 
        WHERE id = @quizId
    ", new { quizId });

        var rows = await db.QueryAsync(@"
       SELECT 
    q.id AS questionid,
    q.text AS questiontext,
    c.id AS choiceid,
    c.text AS choicetext
FROM question q
LEFT JOIN choice c ON q.id = c.questionid
WHERE q.quizid = @quizId
ORDER BY q.id
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
    // RESULTS
    // =====================
    public async Task<IEnumerable<QuizResultDto>> GetQuizResults(int quizId)
    {
        var sql = @"
        SELECT id, fullname, score, createdat
FROM studentattempt
WHERE quizid = @quizId
ORDER BY id DESC;
        ";

        using var db = Connection;
        return await db.QueryAsync<QuizResultDto>(sql, new { quizId });
    }

    // =====================
    // RESULT DETAILS
    // =====================
    public async Task<object> GetResultDetails(int resultId)
    {
        using var db = Connection;

        var attempt = await db.QueryFirstOrDefaultAsync(@"
         SELECT id, fullname, score, createdat
FROM studentattempt

            WHERE id = @resultId
        ", new { resultId });

        if (attempt == null)
            return null;

        var rows = await db.QueryAsync(@"
           SELECT 
    q.id AS questionid,
    q.text AS questiontext,
    c.id AS choiceid,
    c.text AS choicetext,
    c.iscorrect,
    sa.choiceid AS selectedchoiceid
FROM question q
LEFT JOIN choice c ON q.id = c.questionid
LEFT JOIN studentanswer sa 
    ON sa.questionid = q.id AND sa.attemptid = @resultId
WHERE q.quizid = (
    SELECT quizid FROM studentattempt WHERE id = @resultId
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