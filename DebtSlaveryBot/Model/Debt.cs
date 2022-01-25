using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtSlaveryBot.Model
{
    public class Debt
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public virtual User From { get; set; }

        [Required]
        public virtual User To { get; set; }

        public decimal InitialSum { get; set; }

        public decimal LeftSum { get; set; }

        public bool Paid { get; set; }

        public DateTime TimeStamp { get; set; }

        public string Description { get; set; } 

        public virtual DebtEvent Event { get; set; }

        public override string ToString() => $"[Entity::Debt<{Id}>] [{From.Name} ==> {To.Name}] " +
                                             $"InitialSum: {InitialSum} LeftSum: {LeftSum} " +
                                             $"Active: {(Paid ? "NO" : "YES")} " +
                                             $"TimeStamp: '{TimeStamp}' Event '{Event?.Name}' " +
                                             $"Description: {Description}";
    }
}
