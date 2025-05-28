using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.Methods.Constructor;

[ApiVersion(12)]
static class PrimaryConstructor
{
	public readonly struct Distance(double dx, double dy)
	{
		public readonly double Magnitude { get; } = Math.Sqrt(dx * dx + dy * dy);
		public readonly double Direction { get; } = Math.Atan2(dy, dx);
	}

	public struct MutableDistance(double dx, double dy)
	{
		public readonly double Magnitude => Math.Sqrt(dx * dx + dy * dy);
		public readonly double Direction => Math.Atan2(dy, dx);

		public void Translate(double deltaX, double deltaY) {
			dx += deltaX;
			dy += deltaY;
		}

		public MutableDistance() : this(0, 0) { }
	}

	/// <summary>
	/// Base account
	/// </summary>
	/// <param name="accountID">id</param>
	/// <param name="owner">name</param>
	public class BankAccount(string accountID, string owner)
	{
		public string AccountID { get; } = ValidAccountNumber(accountID)
			? accountID
			: throw new ArgumentException("Invalid account number", nameof(accountID));

		public string Owner { get; } = string.IsNullOrWhiteSpace(owner)
			? throw new ArgumentException("Owner name cannot be empty", nameof(owner))
			: owner;

		public override string ToString() => $"Account ID: {AccountID}, Owner: {Owner}";

		public static bool ValidAccountNumber(string accountID) =>
		accountID?.Length == 10 && accountID.All(c => char.IsDigit(c));
	}

	public class CheckingAccount(string accountID, string owner, decimal overdraftLimit = 0) : BankAccount(accountID, owner)
	{
		public decimal CurrentBalance { get; private set; } = 0;

		public void Deposit(decimal amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive");
			}
			CurrentBalance += amount;
		}

		public void Withdrawal(decimal amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive");
			}
			if (CurrentBalance - amount < -overdraftLimit) {
				throw new InvalidOperationException("Insufficient funds for withdrawal");
			}
			CurrentBalance -= amount;
		}

		public override string ToString() => $"Account ID: {AccountID}, Owner: {Owner}, Balance: {CurrentBalance}";
	}

	public class LineOfCreditAccount : BankAccount
	{
		private readonly decimal _creditLimit;
		public LineOfCreditAccount(string accountID, string owner, decimal creditLimit) : base(accountID, owner) {
			_creditLimit = creditLimit;
		}
		public decimal CurrentBalance { get; private set; } = 0;

		public void Deposit(decimal amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive");
			}
			CurrentBalance += amount;
		}

		public void Withdrawal(decimal amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive");
			}
			if (CurrentBalance - amount < -_creditLimit) {
				throw new InvalidOperationException("Insufficient funds for withdrawal");
			}
			CurrentBalance -= amount;
		}

		public override string ToString() => $"{base.ToString()}, Balance: {CurrentBalance}";
	}

	public class SavingsAccount(string accountID, string owner, decimal interestRate) : BankAccount(accountID, owner)
	{
		public SavingsAccount() : this("default", "default", 0.01m) { }
		public decimal CurrentBalance { get; private set; } = 0;

		public void Deposit(decimal amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive");
			}
			CurrentBalance += amount;
		}

		public void Withdrawal(decimal amount) {
			if (amount < 0) {
				throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive");
			}
			if (CurrentBalance - amount < 0) {
				throw new InvalidOperationException("Insufficient funds for withdrawal");
			}
			CurrentBalance -= amount;
		}

		public void ApplyInterest() {
			CurrentBalance *= 1 + interestRate;
		}

		public override string ToString() => $"Account ID: {base.AccountID}, Owner: {base.Owner}, Balance: {CurrentBalance}";
	}
}

