using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DebtSlaveryBot.Helpers;
using System.Linq;

namespace DebtSlaveryBot.Model
{
    public interface IDebtManager
    {
        Debt AddDebt(string fromUser, string toUser, decimal sum, string description = null, string _event = null);
        void ShareSum(decimal sum, string payer, string _event, string description = null);


        User AddUser(string name, TgDetails details = null);
        DebtEvent AddEvent(string name, List<string> users);

        void LinkUserToEvent(string name, string _event);


        void WriteOffDebts(string from, string to, string _event = null);
        void WriteOffDebt(int debtId);
        void WriteOffDebtPartially(int debtId, decimal sum);


        DebtEvent GetEvent(string _event);
        User GetUser(string name);
        User GetUser(long telegramId);
        void SetImpersonalMode(long tgId, bool isImpersonal);

        decimal GetTotalDebtSum(string from, string to);
        List<Debt> GetActiveDebts(string from, string to, string _event = null);

        Dictionary<User, decimal> GetBorrowers(string creditor);
        Dictionary<User, decimal> GetBorrowers(string creditor, string _event);

        Dictionary<User, decimal> GetCreditors(string borrower);
        Dictionary<User, decimal> GetCreditors(string borrower, string _event);

        void ClearStorage();
    }

    public class DebtManager : IDebtManager
    {
        private ILogger<DebtManager> _logger;
        private IServiceProvider _services;

        private DebtDbContext DbContext => _services.GetService<DebtDbContext>();

        public DebtManager(ILogger<DebtManager> logger, IServiceProvider provider)
        {
            _logger = logger;
            _services = provider;
        }

        TotalDebt GetTotalRecord(DebtDbContext ctx, User _from, User _to)
        {
            var queryResult = from t in ctx.TotalDebts
                              where (_from == t.From || _from == t.To) && (_to == t.From || _to == t.To)
                              select t;
            return queryResult.FirstOrDefault();
        }

        public User GetUser(string name) => GetUser(DbContext, name, true);

        public User GetUser(long telegramId)
        {
            var db = DbContext;
            return db.Users.Where(i => i.TgDetails != null && i.TgDetails.Id == telegramId).FirstOrDefault();
        }

        public DebtEvent GetEvent(string _event) => DbContext.Events.FirstOrDefault(e => e.Name == _event);

        private User GetUser(DebtDbContext ctx, string name, bool nullable = false)
        {
            return nullable ?
                    ctx.Users.FirstOrDefault(u => u.Name == name) :
                    ctx.Users.First(u => u.Name == name);
        }
        
        public void SetImpersonalMode(long tgId, bool isImpersonal)
        {
            var user = GetUser(tgId);
            if (user == null)
            {
                throw new DataInconsistentException($"Cannot find user {tgId}");
            }
            _logger.LogInformation($"User {user.Name}, impersonal mode: {isImpersonal}");
            user.TgDetails.ImpersonalMode = isImpersonal;
            DbContext.SaveChanges();
        }

        private (User, User) GetPair(DebtDbContext ctx, string user1, string user2) => (ctx.Users.First(u => u.Name == user1), ctx.Users.First(u => u.Name == user2));

        private Debt AddDebt(DebtDbContext db, User from, User to, decimal sum, string description, DebtEvent debtEvent)
        {
            _logger.LogInformation($"New debt ({sum}) from '{from.Name}' to '{to.Name}', linked event: {debtEvent?.Name}");
            if (sum <= 0)
            {
                throw new DataInconsistentException("Sum must be positive");
            }
            if (from == to)
            {
                throw new DataInconsistentException("From/To must differ!");
            }
            if (debtEvent != null && !debtEvent.Users.ContainsAll(from, to))
            {
                throw new DataInconsistentException($"event: {debtEvent.Name} found invalid users (u1={from.Name}, u2={to.Name})!");
            }

            var debt = new Debt
            {
                From = from,
                To = to,
                Event = debtEvent,
                Description = description,
                InitialSum = sum,
                LeftSum = sum,
                TimeStamp = DateTime.Now
            };
            db.Debts.Add(debt);

            var totalDebt = GetTotalRecord(db, from, to);
            if (totalDebt == null)
            {
                totalDebt = new TotalDebt { From = from, To = to, Sum = 0 };
                db.TotalDebts.Add(totalDebt);
            }
            totalDebt.IncreaseSum(from, debt.InitialSum);
            _logger.LogDebug($"Debt stat from {debt.From.Name} to {debt.To.Name} is now {totalDebt.GetSum(debt.From)}");

            return debt;
        }

        public Debt AddDebt(string fromUser, string toUser, decimal sum, string description = null, string _event = null)
        {
            var db = DbContext;

            var (_from, _to) = GetPair(db, fromUser, toUser);
            DebtEvent _debtEvent = _event == null ? null : db.Events.First(e => e.Name == _event);

            var debt = AddDebt(db, _from, _to, sum, description, _debtEvent);

            db.SaveChanges();

            return debt;
        }

        public User AddUser(string name, TgDetails details = null)
        {
            var db = DbContext;

            _logger.LogInformation($"New user {name}");
            var user = new User { Name = name, TgDetails = details };
            if (details != null)
            {
                db.TgUsers.Add(details);
            }
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        public DebtEvent AddEvent(string name, List<string> users)
        {
            var db = DbContext;

            _logger.LogInformation($"Adding event {name}, {users?.Count} users included");

            List<User> _users = null;
            if (users != null)
            {
                _users = db.Users.Where(u => users.Contains(u.Name)).ToList();
            }
            else
            {
                _users = new List<User>();
            }

            if (users != null && _users.Count != users.Count)
            {
                throw new DataInconsistentException($"Invalid members list passed ({users.Count - _users.Count} non-existent users provided)");
            }

            var _event = new DebtEvent
            {
                Name = name,
                Users = _users
            };

            db.Events.Add(_event);
            db.SaveChanges();

            return _event;
        }

        public decimal GetTotalDebtSum(string from, string to)
        {
            var db = DbContext;

            var (_from, _to) = GetPair(db, from, to);

            decimal result = 0;
            var totalDebt = GetTotalRecord(db, _from, _to);
            if (totalDebt != null)
            {
                result = totalDebt.GetSum(_from);
                if (result < 0)
                {
                    result = 0;
                }
            }
            else 
                _logger.LogDebug($"No debts history between {from} and {to}");

            return result;
        }

        private List<Debt> GetActiveDebts(DebtDbContext db, User _from, User _to, string _event)
        {
            bool DebtUserQuery(Debt d) => !d.Paid && d.From == _from && d.To == _to;

            if (_event != null)
            {
                var evt = db.Events.First(e => e.Name == _event);
                bool DebtEventQuery(Debt d) => DebtUserQuery(d) && d.Event == evt;
                return db.Debts.Where(DebtEventQuery).ToList();
            }
            else
            {
                return db.Debts.Where(DebtUserQuery).ToList();
            }
        }

        public List<Debt> GetActiveDebts(string from, string to, string _event = null)
        {
            var db = DbContext;

            var (_from, _to) = GetPair(db, from, to);

            return GetActiveDebts(db, _from, _to, _event);
        }

        public void WriteOffDebts(string from, string to, string _event = null)
        {
            var db = DbContext;

            _logger.LogDebug($"Writing off debts from '{from}' to '{to}', linked event: '{_event}'");
            var (_from, _to) = GetPair(db, from, to);
            var _total = GetTotalRecord(db, _from, _to);

            var debts = GetActiveDebts(db, _from, _to, _event);
            
            foreach (var debt in debts)
            {
                _total.DecreaseSum(_from, debt.LeftSum);

                debt.Paid = true;
                debt.LeftSum = 0;
            }

            db.SaveChanges();
        }

        public void WriteOffDebt(int debtId)
        {
            var db = DbContext;
            _logger.LogDebug($"Writing off single debt ID {debtId}");

            var debt = db.Debts.First(d => d.Id == debtId);

            if (debt.Paid)
            {
                throw new DataInconsistentException($"Cannot write off debt {debt.Id}: already paid!");
            }

            var total = GetTotalRecord(db, debt.From, debt.To);
            debt.Paid = true;
            _logger.LogDebug($"Debt {debtId} paid");
            total.DecreaseSum(debt.From, debt.LeftSum);
            debt.LeftSum = 0;

            _logger.LogDebug($"Debt stat from {debt.From.Name} to {debt.To.Name} is now {total.GetSum(debt.From)}");

            db.SaveChanges();
        }

        public void WriteOffDebtPartially(int debtId, decimal sum)
        {
            var db = DbContext;
            _logger.LogDebug($"Writing off part of single debt ID {debtId}, value: {sum}");

            var debt = db.Debts.First(d => d.Id == debtId);
            
            if(sum <= 0)
            {
                throw new DataInconsistentException($"Cannot write off {sum} from debt {debt.Id} (positive number requried)");
            }
            if (sum > debt.LeftSum)
            {
                throw new DataInconsistentException($"Cannot write off {sum} from debt {debt.Id} ({debt.LeftSum} left)");
            }

            debt.LeftSum -= sum;
            if (debt.LeftSum <= 0)
            {
                _logger.LogDebug($"Debt {debtId} paid");
                debt.Paid = true;
            }

            var total = GetTotalRecord(db, debt.From, debt.To);
            total.DecreaseSum(debt.From, sum);
            
            _logger.LogDebug($"Debt stat from {debt.From.Name} to {debt.To.Name} is now {total.GetSum(debt.From)}");

            db.SaveChanges();
        }

        public void ShareSum(decimal sum, string payer, string _event, string description = null)
        {
            var db = DbContext;
            _logger.LogInformation($"Sharing {sum} between event '{_event}' members");

            User _payer = GetUser(payer);
            DebtEvent debtEvent = db.Events.First(e => e.Name == _event);

            if (!debtEvent.Users.Contains(_payer))
            {
                throw new DataInconsistentException($"User {_payer.Name} does not belong to event {_event}");
            }

            decimal sharedSum = sum / debtEvent.Users.Count;
            // How to round up sum? Incoming parameter?
            // sharedSum = Math.Round(sharedSum, 2);

            _logger.LogDebug($"Sharing: {sharedSum} per person ({debtEvent.Users.Count - 1} users involved)");

            foreach (var borrower in debtEvent.Users.Where(u => u != _payer))
            {
                AddDebt(db, borrower, _payer, sharedSum, description, debtEvent);
            }

            db.SaveChanges();
        }


        private Dictionary<User, decimal> GetAllRelatedTotals(string user, bool isCreditor)
        {
            var db = DbContext;

            var _user = GetUser(user);
            var relatedTotals = from total in db.TotalDebts
                                where (total.From == _user) || (total.To == _user)
                                select total;

            var results = new Dictionary<User, decimal>();
            foreach (var record in relatedTotals)
            {
                var sum = record.GetSum(_user);
                sum = isCreditor ? -sum : sum;
                if (sum > 0)
                    results.Add(record.GetOther(_user), Math.Abs(sum));
            }
            return results;
        }

        private Dictionary<User, decimal> GetRelatedTotalsInEvent(string user, string _event, bool isCreditor)
        {
            var db = DbContext;

            var _user = GetUser(user);
            var _debtEvent = db.Events.First(e => e.Name == _event);

            var result = new Dictionary<User, decimal>();

            foreach (var related in _debtEvent.Users.Where(u => u != _user))
            {
                var (from, to) = isCreditor ? (related, _user) : (_user, related);

                var debts = GetActiveDebts(db, from, to, _event);

                if (debts.Any())
                {
                    result.Add(related, debts.Sum(d => d.LeftSum));
                }
            }

            return result;
        }


        public Dictionary<User, decimal> GetBorrowers(string creditor) => GetAllRelatedTotals(creditor, true);

        public Dictionary<User, decimal> GetBorrowers(string creditor, string _event) => GetRelatedTotalsInEvent(creditor, _event, true);

        public Dictionary<User, decimal> GetCreditors(string borrower) => GetAllRelatedTotals(borrower, false);

        public Dictionary<User, decimal> GetCreditors(string borrower, string _event) => GetRelatedTotalsInEvent(borrower, _event, false);


        public void LinkUserToEvent(string name, string _event)
        {
            var db = DbContext;

            _logger.LogDebug($"Linking user '{name}' to event '{_event}'");

            var user = GetUser(name);
            var debtEvent = db.Events.First(e => e.Name == _event);

            if (debtEvent.Users.Contains(user))
            {
                throw new DataInconsistentException($"User '{user.Name}' is already linked to event '{debtEvent.Name}'");
            }

            debtEvent.Users.Add(user);

            db.SaveChanges();
        }


        public void ClearStorage() => DeleteAllFromDb(DbContext);

        // Debug methods
        void DeleteAllFromDb(DebtDbContext ctx) =>
            ctx.DeleteAll<User>()
               .DeleteAll<DebtEvent>()
               .DeleteAll<Debt>()
               .DeleteAll<TotalDebt>()
               .DeleteAll<TgDetails>()
               .SaveChanges();

        //void LogTable<T>(string header, Microsoft.EntityFrameworkCore.DbSet<T> set)
        //    where T : class
        //{
        //    _logger.LogInformation($"===== {header} =====");
        //    foreach (var user in set.ToList())
        //        _logger.LogInformation($"{user}");
        //    _logger.LogInformation($"===== {header} END =====\n");
        //}

        //void DisplayDb(DebtDbContext ctx)
        //{
        //    _logger.LogInformation("\n*** DATABASE CONTENT ***");
        //    LogTable("USERS", ctx.Users);
        //    LogTable("DEBTS", ctx.Debts);
        //    LogTable("TOTAL_DEBTS", ctx.TotalDebts);
        //    LogTable("DEBT_EVENTS", ctx.Events);
        //    _logger.LogInformation("*** DATABASE CONTENT END ***\n");
        //}
    }
}
