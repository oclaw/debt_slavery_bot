using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DebtSlaveryBot.Helpers;

namespace DebtSlaveryBot.Model
{
    public class TotalDebt
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public virtual User From { get; set; }

        [Required]
        public virtual User To { get; set; }

        public decimal Sum { get; set; }

        public void IncreaseSum(User target, decimal sum)
        {
            if (!target.IsOneOf(From, To))
            {
                throw new DataInconsistentException($"TotalDebt [{From.Name} ==> {To.Name}] bad user passed ({target.Name})");
            }
            Sum = target == To ? Sum - sum : Sum + sum;
        }

        public void DecreaseSum(User target, decimal sum)
        {
            IncreaseSum(target, -sum);
        }

        public decimal GetSum(User target)
        {
            if (!target.IsOneOf(From, To))
            {
                throw new DataInconsistentException($"TotalDebt [{From.Name} ==> {To.Name}] bad user passed ({target.Name})");
            }
            return target == To ? -Sum : Sum;
        }

        public bool ContainsUser(User u) => From == u || To == u;

        public bool ContainsPair(User u1, User u2)
        {
            if  (ReferenceEquals(u1,u2))
            {
                throw new DataInconsistentException($"TotalDebt u1 == u2");
            }
            return ContainsUser(u1) && ContainsUser(u2);
        }

        public User GetOther(User vs)
        {
            if (vs == From)
            {
                return To;
            }
            else if (vs == To)
            {
                return From;
            }
            throw new DataInconsistentException("TotalDebt: vs not belongs to entity");
        }

        public override string ToString() => $"[Entity::TotalDebt<{Id}>] [{From.Name} ==> {To.Name}] Sum: {Sum}";
    }
}
