namespace QuizAPI.Models;

public class SubmitAnswerRequest
{
    public int AttemptId { get; set; }
    public int QuestionId { get; set; }
    public int ChoiceId { get; set; }
}