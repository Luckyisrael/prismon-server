using System.ComponentModel.DataAnnotations;

namespace Prismon.Api.DTOs;

public class CreateAppRequest
{
    [Required(ErrorMessage = "App name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be 3-100 characters")]
    public string Name { get; set; } = string.Empty;
}