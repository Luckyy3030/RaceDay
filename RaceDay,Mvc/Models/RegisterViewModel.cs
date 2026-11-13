
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.Models;

public class RegisterViewModel
{
    [Required]
    public string FirstName { get; set; } = "";

    [Required]
    public string LastName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required]
    [Compare("Password")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    public string Role { get; set; } = "Participant";

    public string? OrganisationName { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    public string? EmergencyContact { get; set; }
}
