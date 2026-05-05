namespace QuizAPI.Models;

public class StudentAnswer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public int QuestionId { get; set; }
    public int ChoiceId { get; set; }
    public bool IsCorrect { get; set; }
}