using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DebtSlaveryBot.Model
{
    public class User
    {

        public User()
        {

        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public virtual TgDetails TgDetails { get; set; }
        public virtual ICollection<DebtEvent> DebtEvents { get; set; }  

        public override string ToString() => $"[Entity::User<{Id}>] Name: '{Name}'";

        public string TgName 
        {
            get
            {
                if (TgDetails == null)
                {
                    return Name;
                }
                if (!string.IsNullOrWhiteSpace(TgDetails.UserName))
                {
                    return $"@{TgDetails.UserName}";
                }
                return $"{TgDetails.FirstName} {TgDetails.LastName}";
            }
        }
    }
}
