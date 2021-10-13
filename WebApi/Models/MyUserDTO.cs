using System;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Models
{
    public class MyUserDTO
    {
        [Required]
        public string Login { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}