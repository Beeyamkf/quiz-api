namespace QuizAPI.Models;

public class CreateQuestionWithChoicesRequest
{
    public int QuizId { get; set; }
    public string Text { get; set; }

    public List<CreateChoiceDto> Choices { get; set; }
}

public class CreateChoiceDto
{
    public string Text { get; set; }
    public bool IsCorrect { get; set; }
}