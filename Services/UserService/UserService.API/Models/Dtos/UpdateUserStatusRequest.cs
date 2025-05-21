using System.ComponentModel.DataAnnotations;
using UserService.API.Models.Enums;

namespace UserService.API.Models.Dtos
{
    public class UpdateUserStatusRequest
    {
        [Required]
        public UserStatus Status { get; set; }
    }
}