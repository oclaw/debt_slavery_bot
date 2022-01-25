using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using DebtSlaveryBot.Model;

namespace DebtSlaveryBotTests
{
    class MockServiceProvider : IServiceProvider
    {
        public List<object> Services = new List<object>();

        public object GetService(Type type) => Services.FirstOrDefault(svc => svc.GetType() == type);

        public object GetService<T>() where T : class => GetService(typeof(T));
    }

    public class DebtManagerTests
    {
        IDebtManager Manager;

        private string Str(string s)
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            var currentMethodName = sf.GetMethod();

            return $"<{currentMethodName}>_{s}";
        }


        [SetUp]
        public void Setup()
        {

            var dbOptions = new DbContextOptionsBuilder<DebtDbContext>()
                .UseInMemoryDatabase("DebtSlaveryBot-InMemoryDB")
                .Options;

            //var dbOptions = new DbContextOptionsBuilder<DebtDbContext>()
            //    .UseNpgsql("Host=192.168.1.37;Database=dbslave_dev;Username=dbslave_bot;Password=*******")
            //    .UseSnakeCaseNamingConvention()
            //    .Options;

            var dbContext = new DebtDbContext(dbOptions);

            MockServiceProvider provider = new MockServiceProvider()
            {
                Services = { dbContext }
            };

            Manager = new DebtManager(new NullLogger<DebtManager>(), provider);

            Manager.ClearStorage();
        }

        [Test]
        public void TestAddUser()
        {
            string TestUserName = Str("TestUser");
            var user = Manager.AddUser(TestUserName);

            Assert.NotNull(user);
            Assert.AreEqual(TestUserName, user.Name);

            var got = Manager.GetUser(TestUserName);
            Assert.AreEqual(user.Name, got.Name);
        }

        [Test]
        public void TestAddDebt()
        {
            string firstUser = Str("first"), secondUser = Str("second");
            var user1 = Manager.AddUser(firstUser);
            var user2 = Manager.AddUser(secondUser);

            const double DebtSum = 100;
            const string TestDescription = "Test Description";
            var debt = Manager.AddDebt(firstUser, secondUser, 100, TestDescription, null);

            Assert.NotNull(debt);
            Assert.NotNull(debt.From);
            Assert.NotNull(debt.To);
            Assert.Null(debt.Event);

            Assert.AreEqual(debt.From.Name, user1.Name);
            Assert.AreEqual(debt.To.Name, user2.Name);
            Assert.AreEqual(debt.InitialSum, DebtSum);
            Assert.AreEqual(debt.InitialSum, debt.LeftSum);
            Assert.AreEqual(debt.Description, TestDescription);
        }

        [Test]
        public void TestCrossDebt()
        {
            string user1 = Str("Nick");
            string user2 = Str("Mike");

            void AssertDebtSums(double expectU1U2, double expectU2U1)
            {
                var u1_u2 = Manager.GetTotalDebtSum(user1, user2);
                var u2_u1 = Manager.GetTotalDebtSum(user2, user1);

                Assert.AreEqual(expectU1U2, u1_u2);
                Assert.AreEqual(expectU2U1, u2_u1);
            }

            Manager.AddUser(user1);
            Manager.AddUser(user2);

            Manager.AddDebt(user1, user2, 500, "Day 1 dinner");

            AssertDebtSums(500, 0);

            Manager.AddDebt(user2, user1, 600, "Day 2 dinner");

            AssertDebtSums(0, 100);

            string user3 = Str("Stasyan");
            Manager.AddUser(user3);

            decimal notExistingDebt = Manager.GetTotalDebtSum(user1, user3);
            Assert.AreEqual(0, notExistingDebt);
        }

        [Test]
        public void TestAddEvent()
        {
            string user1 = Str("User1");
            string evt = Str("Event1");
            string evt2 = Str("Event2");


            var user = Manager.AddUser(user1);
            var _event = Manager.AddEvent(evt, new List<string> { user1 });

            Assert.NotNull(_event);
            Assert.AreEqual(_event.Name, evt);
            Assert.NotNull(_event.Users);
            Assert.AreEqual(_event.Users.Count, 1);
            Assert.AreEqual(_event.Users.First(), user);


            var _event2 = Manager.AddEvent(evt2, new List<string> { user1 });

            Assert.True(Enumerable.SequenceEqual(_event.Users, _event2.Users));


            string badEventName = Str("BadEvent");
            Assert.Throws<DataInconsistentException> (() => Manager.AddEvent(badEventName, new List<string> { user1, "non_existent_user" }));
        }

        [Test]
        public void TestGetActiveDebts()
        {
            string user1 = Str("Nick"), user2 = Str("Mike");

            Manager.AddUser(user1);
            Manager.AddUser(user2);

            void AddDebt(string from, string to, List<Debt> localRecords, decimal sum, string _event = null)
            {
                var debt = Manager.AddDebt(from, to, sum, "", _event);
                localRecords.Add(debt);
            }

            List<Debt> u1ToU2 = new List<Debt>(), 
                       u2ToU1 = new List<Debt>(),
                       DB_u1ToU2, DB_u2ToU1;

            DB_u1ToU2 = Manager.GetActiveDebts(user1, user2);
            Assert.AreEqual(0, DB_u1ToU2.Count);

            DB_u2ToU1 = Manager.GetActiveDebts(user2, user1);
            Assert.AreEqual(0, DB_u2ToU1.Count);


            AddDebt(user1, user2, u1ToU2, 500);
            AddDebt(user2, user1, u2ToU1, 100);

            DB_u1ToU2 = Manager.GetActiveDebts(user1, user2);
            Assert.True(Enumerable.SequenceEqual(u1ToU2, DB_u1ToU2));

            DB_u2ToU1 = Manager.GetActiveDebts(user2, user1);
            Assert.True(Enumerable.SequenceEqual(u2ToU1, DB_u2ToU1));

            string eventName = Str("TestEvent");
            var testEvent = Manager.AddEvent(eventName, new List<string> {user1, user2});
            
            AddDebt(user1, user2, u1ToU2, 501, eventName);
            AddDebt(user2, user1, u2ToU1, 101, eventName);

            IEnumerable<Debt> EventFiltered(List<Debt> debts) => debts.Where(d => d.Event == testEvent);

            DB_u1ToU2 = Manager.GetActiveDebts(user1, user2, eventName);
            Assert.True(Enumerable.SequenceEqual(EventFiltered(u1ToU2), DB_u1ToU2));


            DB_u2ToU1 = Manager.GetActiveDebts(user2, user1, eventName);
            Assert.True(Enumerable.SequenceEqual(EventFiltered(u2ToU1), DB_u2ToU1));
        }

        private static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        [Test]
        public void TestWriteOffDebts()
        {
            string user1 = Str("Nick"), user2 = Str("Mike");

            Manager.AddUser(user1);
            Manager.AddUser(user2);

            decimal TestDebtSum = 500;
            Manager.AddDebt(user1, user2, TestDebtSum);

            decimal debtSum;

            debtSum = Manager.GetTotalDebtSum(user1, user2);
            Assert.AreEqual(TestDebtSum, debtSum);

            Manager.WriteOffDebts(user1, user2);
            
            debtSum = Manager.GetTotalDebtSum(user1, user2);
            Assert.AreEqual(0, debtSum);


            var rng = new Random();
            bool TimeToSwap() => (rng.Next() % 3) == 0;

            string[] users = { user1, user2 };


            decimal directSum, invertSum;
            List<Debt> directDebts, invertDebts;
            List<Debt> allDebts = new List<Debt>();

            do
            {
                for (var i = 0; i < 100; i++)
                {
                    if (TimeToSwap())
                    {
                        Swap(ref users[0], ref users[1]);
                    }
                    var debt = Manager.AddDebt(users[0], users[1], rng.Next(1, 100));
                    allDebts.Add(debt);
                }

                directSum = Manager.GetTotalDebtSum(user1, user2);
                invertSum = Manager.GetTotalDebtSum(user2, user1);

                directDebts = Manager.GetActiveDebts(user1, user2);
                invertDebts = Manager.GetActiveDebts(user2, user1);
            } while ((directSum == 0 && invertSum == 0) || !directDebts.Any() || !invertDebts.Any());

            // continue adding debts until its unbalanced and contains bidirectional unpaid debts


            void WriteOffAndCheck(string borrower, string creditor, decimal directSum, decimal invertSum)
            {
                Assert.AreEqual(0, invertSum);
                Assert.AreNotEqual(0, directSum);

                Manager.WriteOffDebts(borrower, creditor);

                var directDebtsAfterWriteOff = Manager.GetActiveDebts(borrower, creditor);
                Assert.False(directDebtsAfterWriteOff.Any());

                var directSumAfterWriteOff = Manager.GetTotalDebtSum(borrower, creditor);
                Assert.AreEqual(0, directSumAfterWriteOff);

                decimal inverseSumAfterWriteOff = Manager.GetTotalDebtSum(creditor, borrower);
                Assert.AreNotEqual(0, inverseSumAfterWriteOff);

                Manager.WriteOffDebts(creditor, borrower);

                var invertDebtsAfterWipeOut = Manager.GetActiveDebts(creditor, borrower);
                Assert.False(invertDebtsAfterWipeOut.Any());

                decimal invertSumAfterWipeOut = Manager.GetTotalDebtSum(creditor, borrower);
                Assert.AreEqual(0, invertSumAfterWipeOut);
            }


            if (directSum > 0)
            {
                WriteOffAndCheck(user1, user2, directSum, invertSum);
            }
            else
            {
                WriteOffAndCheck(user2, user1, invertSum, directSum);
            }
        }

        [Test]
        public void TestWriteOffDebtsByEvent()
        {
            string user1 = Str("Nick"), user2 = Str("Mike");

            Manager.AddUser(user1);
            Manager.AddUser(user2);

            string eventName = Str("Event");

            var _event = Manager.AddEvent(eventName, new List<string> { user1, user2 });


            decimal debt = 100;
            decimal eventDebt = 200;

            Manager.AddDebt(user1, user2, debt, "", null);
            Manager.AddDebt(user1, user2, eventDebt, "", eventName);

            decimal total = Manager.GetTotalDebtSum(user1, user2);
            Assert.AreEqual(debt + eventDebt, total);

            Manager.WriteOffDebts(user1, user2, eventName);
            
            total = Manager.GetTotalDebtSum(user1, user2);
            Assert.AreEqual(debt, total);
        }

        [Test]
        public void TestWriteOffSingleDebt()
        {
            string user1 = Str("Nick"), user2 = Str("Mike");

            Manager.AddUser(user1);
            Manager.AddUser(user2);

            string event1Name = Str("Event1");

            var users = new List<string> { user1, user2 };
            var _event1 = Manager.AddEvent(event1Name, users);

            decimal debt1Sum = 100;
            var debt1 = Manager.AddDebt(user1, user2, debt1Sum, "", event1Name);

            decimal debt2Sum = 200;
            var debt2 = Manager.AddDebt(user1, user2, debt2Sum, "", event1Name);

            var total = Manager.GetTotalDebtSum(user1, user2);
            Assert.AreEqual(debt1Sum + debt2Sum, total);

            Manager.WriteOffDebt(debt1.Id);

            var debts = Manager.GetActiveDebts(user1, user2);
            Assert.False(debts.Contains(debt1));
            Assert.True(debt1.Paid);

            total = Manager.GetTotalDebtSum(user1, user2);
            Assert.AreEqual(debt2Sum, total);


            decimal partDebtSum = 100;
            var partDebt = Manager.AddDebt(user1, user2, partDebtSum);
            
            Manager.WriteOffDebtPartially(partDebt.Id, partDebtSum / 2);
            Assert.False(partDebt.Paid);
            Assert.AreEqual(partDebt.InitialSum, partDebtSum);
            Assert.AreEqual(partDebt.LeftSum, partDebtSum - (partDebtSum / 2));

            Manager.WriteOffDebtPartially(partDebt.Id, partDebtSum / 2);
            Assert.True(partDebt.Paid);
            Assert.AreEqual(partDebt.LeftSum, 0);


            debts = Manager.GetActiveDebts(user1, user2);
            Assert.False(debts.Contains(partDebt));
        }

        [Test]
        public void TestShareSum()
        {
            List<string> borrowers = new List<string>();
            string creditor = Str("Creditor");

            for (var i = 0; i < 10; i++)
            {
                string borrower = Str($"Borrower#{i}");
                Manager.AddUser(borrower);
                borrowers.Add(borrower);
            }

            Manager.AddUser(creditor);

            string _event = Str("Event");

            List<string> eventMembers = new List<string>();
            eventMembers.AddRange(borrowers);
            eventMembers.Add(creditor);

            Manager.AddEvent(_event, eventMembers);

            decimal sum = 1100;
            Manager.ShareSum(sum, creditor, _event, "");

            foreach (var borrower in borrowers)
            {
                var debts = Manager.GetActiveDebts(borrower, creditor, _event);
                Assert.AreEqual(1, debts.Count);
                Assert.AreEqual(sum / eventMembers.Count, debts.First().LeftSum);
            }
        }

        [Test]
        public void TestGetBorrowers()
        {
            string creditor = Str("Master");
            string borrower1 = Str("Slave#1");
            string borrower2 = Str("Slave#2");
            string eventName = Str("Event");

            Manager.AddUser(creditor);
            var borrower1Obj = Manager.AddUser(borrower1);
            var borrower2Obj = Manager.AddUser(borrower2);

            Manager.AddEvent(eventName, new List<string> { creditor, borrower1, borrower2 });

            decimal borrower1Sum = 100;
            decimal borrower2Sum = 200;

            Manager.AddDebt(borrower1, creditor, borrower1Sum, "", eventName);
            Manager.AddDebt(borrower2, creditor, borrower2Sum, "", eventName);

            var borrowers = Manager.GetBorrowers(creditor, eventName);
            var globalBorrowers = Manager.GetBorrowers(creditor);

            Assert.AreEqual(2, borrowers.Count);
            Assert.True(borrowers.ContainsKey(borrower1Obj));
            Assert.True(borrowers.ContainsKey(borrower2Obj));

            Assert.AreEqual(borrower1Sum, borrowers[borrower1Obj]);
            Assert.AreEqual(borrower2Sum, borrowers[borrower2Obj]);

            Assert.True(Enumerable.SequenceEqual(
                borrowers.OrderBy(u => u.Key.Name),
                globalBorrowers.OrderBy(u => u.Key.Name)
            ));

            foreach (var b in new string[] { borrower1, borrower2 })
            {
                var emptySet = Manager.GetBorrowers(b, eventName);
                var globalEmptySet = Manager.GetBorrowers(b);
                Assert.False(emptySet.Any());
                Assert.False(globalEmptySet.Any());
            }

            decimal globalDebt = 10000;
            Manager.AddDebt(borrower1, creditor, globalDebt, "");

            borrowers = Manager.GetBorrowers(creditor, eventName);
            globalBorrowers = Manager.GetBorrowers(creditor);

            Assert.AreEqual(globalDebt, globalBorrowers[borrower1Obj] - borrowers[borrower1Obj]);
        }

        [Test]
        public void TestGetCreditors()
        {
            string borrower = Str("Slave");
            string creditor1 = Str("Master#1");
            string creditor2 = Str("Master#2");
            string eventName = Str("Event");

            Manager.AddUser(borrower);
            var creditor1Obj = Manager.AddUser(creditor1);
            var creditor2Obj = Manager.AddUser(creditor2);

            Manager.AddEvent(eventName, new List<string> { borrower, creditor1, creditor2 });

            decimal cred1Sum = 100;
            decimal cred2Sum = 200;

            Manager.AddDebt(borrower, creditor1, cred1Sum, "", eventName);
            Manager.AddDebt(borrower, creditor2, cred2Sum, "", eventName);

            var creditors = Manager.GetCreditors(borrower, eventName);
            var globalCreditors = Manager.GetCreditors(borrower);

            Assert.True(Enumerable.SequenceEqual(
                creditors.OrderBy(u => u.Key.Name),
                globalCreditors.OrderBy(u => u.Key.Name)
            ));


            foreach (var b in new string[] { creditor1, creditor2 })
            {
                var emptySet = Manager.GetCreditors(b, eventName);
                var globalEmptySet = Manager.GetCreditors(b);
                Assert.False(emptySet.Any());
                Assert.False(globalEmptySet.Any());
            }

            decimal globalDebt = 10000;
            Manager.AddDebt(borrower, creditor1, globalDebt, "");

            creditors = Manager.GetCreditors(borrower, eventName);
            globalCreditors = Manager.GetCreditors(borrower);

            Assert.AreEqual(globalDebt, globalCreditors[creditor1Obj] - creditors[creditor1Obj]);
        }

        [Test]
        public void TestLinkUserToEvent()
        {
            string user1 = Str("Nick");
            string user2 = Str("Mike");
            string user3 = Str("Stasyan");
            string eventName = Str("Event");

            var _user1 = Manager.AddUser(user1);
            var _user2 = Manager.AddUser(user2);
            var _user3 = Manager.AddUser(user3);

            var _event = Manager.AddEvent(eventName, new List<string> { user1, user2 });
            Assert.True(_event.Users.Contains(_user1));
            Assert.True(_event.Users.Contains(_user2));
            Assert.False(_event.Users.Contains(_user3));

            Manager.LinkUserToEvent(user3, eventName);
            Assert.True(_event.Users.Contains(_user3));
        }
    }
}