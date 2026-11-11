namespace TutoringManager.Api.Demo;

/// <summary>The fixed cast of the demo account. Everything else is generated relative to today.</summary>
public static class DemoData
{
    public record DemoStudent(
        string FullName,
        string Email,
        decimal Rate,
        DayOfWeek Day,
        TimeOnly Start,
        int DurationMinutes,
        string Subject,
        string AccountSeed);

    public static readonly DemoStudent[] Students =
    [
        new("Zofia Wiśniewska", "zofia.wisniewska@example.com", 150m, DayOfWeek.Monday, new(16, 0), 60, "maths", "1140200400003002013"),
        new("Jakub Kowalczyk", "jakub.kowalczyk@example.com", 140m, DayOfWeek.Monday, new(17, 30), 60, "maths", "1020102600000422702"),
        new("Maja Lewandowska", "maja.lewandowska@example.com", 180m, DayOfWeek.Tuesday, new(15, 0), 90, "physics", "2490000500004000300"),
        new("Antoni Zieliński", "antoni.zielinski@example.com", 130m, DayOfWeek.Tuesday, new(17, 0), 60, "maths", "1050001234000090301"),
        new("Lena Szymańska", "lena.szymanska@example.com", 150m, DayOfWeek.Wednesday, new(16, 0), 60, "physics", "1160222200000000123"),
        new("Filip Woźniak", "filip.wozniak@example.com", 110m, DayOfWeek.Wednesday, new(18, 0), 45, "maths", "1240100400001111999"),
        new("Hanna Dąbrowska", "hanna.dabrowska@example.com", 150m, DayOfWeek.Thursday, new(16, 30), 60, "maths", "1870100045000222111"),
        new("Szymon Kamiński", "szymon.kaminski@example.com", 140m, DayOfWeek.Thursday, new(18, 0), 60, "physics", "1090101400000712198"),
        new("Julia Mazur", "julia.mazur@example.com", 120m, DayOfWeek.Friday, new(15, 0), 60, "maths", "1320100900002000100"),
    ];

    public static readonly Dictionary<string, string[]> NoteTopics = new()
    {
        ["maths"] =
        [
            "Quadratic equations: the discriminant and Vieta's formulas. Homework: exercises 4 to 9.",
            "Function transformations, shifts and reflections. Strong on graphs, revise the notation.",
            "Arithmetic and geometric sequences. Homework: two exam tasks from the 2024 paper.",
            "Trigonometry on the unit circle. Mixed up sine and cosine twice; flashcards for next week.",
            "Probability with tree diagrams. Good progress. Homework: exercises 12 to 15.",
            "Mock exam, part one: 31 of 46 points. Next time: the two geometry tasks she skipped.",
            "Logarithms, rules and equations. Needs more practice changing the base.",
            "Plane geometry: similar triangles and the intercept theorem. Homework: three proofs.",
        ],
        ["physics"] =
        [
            "Kinematics: velocity-time graphs and area under the curve. Homework: worksheet 3.",
            "Newton's laws with friction on an incline. Free-body diagrams much cleaner now.",
            "Work, energy and power. Homework: four problems from the textbook, chapter 5.",
            "Circular motion and centripetal force. Revisit units next lesson.",
            "Electric circuits: Ohm's law, series and parallel. Did the lab report together.",
            "Waves: frequency, wavelength, the Doppler effect. Homework: past paper question 7.",
        ],
    };

    /// <summary>A sender account that belongs to no student yet: a parent paying for Hanna.</summary>
    public const string ParentAccountSeed = "1600140200000654321";
    public const string ParentName = "KATARZYNA DĄBROWSKA";
}
