namespace QuizAPI.Models;

public class StudentAttempt
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public string FullName { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }  
}