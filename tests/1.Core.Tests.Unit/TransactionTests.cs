
using _2.Shared.Models;
using Xunit;
using _1.Core.Entities;

namespace _1.Core.Tests.Unit
{
    public class TransactionTests
    {
        [Fact]
        public void TransactionWithZeroAmountIsNotValid()
        { 
            //Arrange
            double invalid_amount = 0;

            Money invalidMoney = GetMoneyInEuros(invalid_amount);

            DateTime dateTime = DateTime.Now;
            string description = string.Empty;

            TransactionCategory category = TransactionCategory.EXPENSE;

            // Act
            Action action = () => { new Transaction(dateTime, description, invalidMoney, category); };


            //Assert
            Assert.Throws<ArgumentException>(() => action());


        }

        [Fact]
        public void ExpenseTransactionWithNegativeAmountIsNotValid()
        {
            //Arrange
            double invalid_amount = -10;

            Money invalidMoney = GetMoneyInEuros(invalid_amount);

            DateTime dateTime = DateTime.Now;
            string description = string.Empty;

            TransactionCategory category = TransactionCategory.EXPENSE;

            // Act
            Action action = () => { new Transaction(dateTime, description, invalidMoney, category); };


            //Assert
            Assert.Throws<ArgumentException>(() => action());


        }
        [Fact]
        public void IncomeTransactionWithPositiveAmountIsNotValid()
        {
            //Arrange
            double invalid_amount = 10;

            Money invalidMoney = GetMoneyInEuros(invalid_amount);

            DateTime dateTime = DateTime.Now;
            string description = string.Empty;

            TransactionCategory category = TransactionCategory.INCOME;

            // Act
            Action action = () => { new Transaction(dateTime, description, invalidMoney, category); };


            //Assert
            Assert.Throws<ArgumentException>(() => action());


        }


        private Money GetMoneyInEuros(double amount)
        {

            return new Money()
            {
                Amount = amount,
                Currency = Currencies.EUR
            };

        }


    }
}
