using System.ComponentModel.DataAnnotations;

namespace TutoringManager.Api.Students;

public record CreateStudentRequest(
    [property: Required, MaxLength(200)] string FullName,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Range(typeof(decimal), "0", "100000")] decimal RatePerLesson,
    List<string>? BankAccounts);

public record UpdateStudentRequest(
    [property: Required, MaxLength(200)] string FullName,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Range(typeof(decimal), "0", "100000")] decimal RatePerLesson,
    bool IsActive);

public record AddBankAccountRequest([property: Required, MaxLength(40)] string AccountNumber);

public record StudentSummary(Guid Id, string FullName, string Email, decimal RatePerLesson, bool IsActive);

public record BankAccountResponse(Guid Id, string AccountNumber);

public record StudentDetails(
    Guid Id,
    string FullName,
    string Email,
    decimal RatePerLesson,
    bool IsActive,
    DateTime CreatedAt,
    List<BankAccountResponse> BankAccounts);
