using System.ComponentModel.DataAnnotations;

namespace Nurtura.Web.Models.Account;

// Names and messages are keys in Resources/en.json and pl.json.
public class LoginViewModel
{
    [Required(ErrorMessage = "Validation_Required")]
    [EmailAddress(ErrorMessage = "Validation_Email")]
    [Display(Name = "Account_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required")]
    [DataType(DataType.Password)]
    [Display(Name = "Account_Password")]
    public string Password { get; set; } = string.Empty;

    // A bool is always required by MVC; naming the attribute here makes its message come from the JSON files too.
    [Required(ErrorMessage = "Validation_Required")]
    [Display(Name = "Account_RememberMe")]
    public bool RememberMe { get; set; }
}
