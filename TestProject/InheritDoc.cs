﻿using System;

namespace TestProject
{
	[Serializable]
	public class MyException : Exception
	{
		// hover on following constructor methods to see XML Doc from type Exception
		public MyException() { }
		public MyException(string message) : base(message) { }
		public MyException(string message, Exception inner) : base(message, inner) { }
		protected MyException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	abstract class Walker : IWalk
	{
		/// <inheritdoc cref="InternalWalk"/>
		public abstract void Walk();

		/// <summary>
		/// Internal walk.
		/// </summary>
		void InternalWalk() { }
	}
	abstract class Singer : Walker, ISing
	{
		public abstract void Sing(string title, params string[] lyrics);

		/// <summary>
		/// Do something.
		/// </summary>
		/// <returns>Something.</returns>
		protected abstract object Do();
	}
	interface ISing
	{
		/// <summary>
		/// Sing a song.
		/// </summary>
		/// <param name="title">The title of the song.</param>
		/// <param name="lyrics">The lyrics.</param>
		void Sing(string title, params string[] lyrics);
	}
	interface IRun
	{
		/// <summary>
		/// Run for a while.
		/// </summary>
		void Run();
	}
	interface IWalk
	{
		/// <summary>
		/// Walk a minute.
		/// </summary>
		void Walk();
	}

	class TestInheritDoc : Singer, IRun, IWalk
	{
		/// <summary>
		/// The description of method.
		/// </summary>
		/// <param name="param">The description of parameter.</param>
        /// <returns>A value.</returns>
		public static int Method(string param) {
            return 1;
		}

		// turn on <inheritdoc cref=""/> feature in the options page of Super Quick Info,
		// hover on the following method to see it inherit doc from the above method
		/// <inheritdoc cref="Method(string)"/>
		/// <param name="value">The value of the method.</param>
		public void InheritFromMethod(string param, string value) {

		}

		// hover on Do to see its inherited documentation
		protected override object Do() {
            return null;
		}

		// hover on Run to see its inherited documentation
		public void Run() {
		}
		// this does not inherited from documentation of IRun
		public void Run(int mile) { }
		// this does not inherited from documentation of IRun
		public void Run<T>() { }

		// hover on Walk to see its inherited documentation
		void IWalk.Walk() {
		}

		// hover on ToString to see its inherited documentation
		public override string ToString() {
			return "TEST";
		}

		// hover on Sing, title, lyrics to see their inherited documentations
		public override void Sing(string title, params string[] lyrics) {
		}

		// hover on Walk to see its inherited doc from Walker.InternalWalk
		public override void Walk() {
			Sing("untitled", "a", "b");
		}
	}
}
