using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyPoint = (int x, int y);

namespace TestProject.CS12_0;

internal static class Sample
{
	static void CollectionExpression() {
		// Create an array:
		int[] a = [1, 2, 3, 4, 5, 6, 7, 8];

		// Create a list:
		List<string> b = ["one", "two", "three"];

		// Create a span
		Span<char> c = ['a', 'b', 'c', 'd', 'e', 'f', 'h', 'i'];

		// Create a jagged 2D array:
		int[][] twoD = [[1, 2, 3], [4, 5, 6], [7, 8, 9]];

		// Create a jagged 2D array from variables:
		int[] row0 = [1, 2, 3];
		int[] row1 = [4, 5, 6];
		int[] row2 = [7, 8, 9];
		int[][] twoDFromVariables = [row0, row1, row2];

		int[] single = [.. row0, .. row1, .. row2];
		foreach (var element in single) {
			Console.Write($"{element}, ");
		}
	}

	static void LambdaWithDefault() {
		var expression = (int source, int increment = 1) => source + increment;

		Console.WriteLine(expression(5)); // 6
		Console.WriteLine(expression(5, 2)); // 7
	}

	static void LambdaWithParams() {
		var sum = (params int[] values) =>
		{
			int sum = 0;
			foreach (var value in values)
				sum += value;

			return sum;
		};

		var empty = sum();
		Console.WriteLine(empty); // 0

		var sequence = new[] { 1, 2, 3, 4, 5 };
		var total = sum(sequence);
		Console.WriteLine(total); // 15
	}

	static void AnyAlias() {
		ValueTuple<int, int> p = new MyPoint(0, 0);
		MyPoint point = new MyPoint(0, 0);
		point.x = 1;
		point.y = 2;
	}

	delegate int IncrementByDelegate(int source, int increment = 1);
	delegate int SumDelegate(params int[] values);
}

class RefReadonly
{
	void M1(I1 o, ref readonly int x) => System.Console.Write("1");
	void M2(I2 o, ref int x) => System.Console.Write("2");
	void Run() {
		D1 m1 = M1;
		D2 m2 = M2;

		var i = 5;
		m1(null, in i);
		m2(null, ref i);
	}
	static void Main() => new RefReadonly().Run();

	interface I1 { }
	interface I2 { }
	class X : I1, I2 { }
	delegate void D1(X s, ref readonly int x);
	delegate void D2(X s, ref int x);
}

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
