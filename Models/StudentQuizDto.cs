namespace QuizAPI.Models;

public class StudentQuizDto
{
    public string Title { get; set; } = "";
    public List<StudentQuestionDto> Questions { get; set; } = new();
}

public class StudentQuestionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public List<StudentChoiceDto> Choices { get; set; } = new();
}

public class StudentChoiceDto
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
}