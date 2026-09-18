using System.ComponentModel.DataAnnotations;

namespace RentalManagement.Web.Models.Account;

public class RegisterViewModel
{
    [Required, StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "I am registering as")]
    public string Role { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
