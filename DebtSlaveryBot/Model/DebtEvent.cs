using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DebtSlaveryBot.Model
{
    public class DebtEvent
    {
        public DebtEvent()
        {

        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public virtual ICollection<User> Users { get; set; }

        private string StringifyMembers()
        {
            if (Users == null || !Users.Any())
                return "None";

            return string.Join(", ", Users.Select(m => m.Name));
        }

        public override string ToString() => $"[Entity::DebtEvent<{Id}>] Name: '{Name}' Members: [{StringifyMembers()}]";
    }
}
