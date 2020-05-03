using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.CS7_2
{
    #region Safe efficient code
    // see: https://docs.microsoft.com/en-us/dotnet/csharp/write-safe-efficient-code
    readonly public struct ReadonlyPoint3D
    {
        public ReadonlyPoint3D(double x, double y, double z) {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }

    public struct Point3D
    {
        static readonly Point3D Empty = new Point3D();
        private static Point3D origin = new Point3D(0, 0, 0);

        public Point3D(double x, double y, double z) {
            _x = x;
            _y = y;
            _z = z;
        }

        private double _x;
        public double X {
            readonly get => _x;
            set => _x = value;
        }

        private double _y;
        public double Y {
            readonly get => _y;
            set => _y = value;
        }

        private double _z;
        public double Z {
            readonly get => _z;
            set => _z = value;
        }

        public readonly double Distance => Math.Sqrt(X * X + Y * Y + Z * Z);
        public static ref readonly Point3D Origin => ref origin;

        public readonly override string ToString() => $"{X}, {Y}, {Z}";
    }

    class CallingSite
	{
        void M() {
            var originValue = Point3D.Origin;
            ref readonly var originReference = ref Point3D.Origin;
            Point3D pt1 = new Point3D(), pt2 = new Point3D();
            var distance = CalculateDistance(pt1, pt2);
            var fromOrigin = CalculateDistance(pt1, new Point3D());
            distance = CalculateDistance(in pt1, in pt2);
            distance = CalculateDistance(in pt1, new Point3D());
            distance = CalculateDistance(pt1, in Point3D.Origin);
        }

        private static double CalculateDistance(in Point3D point1, in Point3D point2) {
            double xDifference = point1.X - point2.X;
            double yDifference = point1.Y - point2.Y;
            double zDifference = point1.Z - point2.Z;

            return Math.Sqrt(xDifference * xDifference + yDifference * yDifference + zDifference * zDifference);
        }
        private static double CalculateDistance2(in Point3D point1, in Point3D point2 = default) {
            double xDifference = point1.X - point2.X;
            double yDifference = point1.Y - point2.Y;
            double zDifference = point1.Z - point2.Z;

            return Math.Sqrt(xDifference * xDifference + yDifference * yDifference + zDifference * zDifference);
        }
        private static double CalculateDistance3(in ReadonlyPoint3D point1, in ReadonlyPoint3D point2 = default) {
            double xDifference = point1.X - point2.X;
            double yDifference = point1.Y - point2.Y;
            double zDifference = point1.Z - point2.Z;

            return Math.Sqrt(xDifference * xDifference + yDifference * yDifference + zDifference * zDifference);
        }
    }
    #endregion

    #region Named and optional arguments
    class NamedExample
    {
        static void Main(string[] args) {
            // The method can be called in the normal way, by using positional arguments.
            PrintOrderDetails("Gift Shop", 31, "Red Mug");

            // Named arguments can be supplied for the parameters in any order.
            PrintOrderDetails(orderNum: 31, productName: "Red Mug", sellerName: "Gift Shop");
            PrintOrderDetails(productName: "Red Mug", sellerName: "Gift Shop", orderNum: 31);

            // Named arguments mixed with positional arguments are valid
            // as long as they are used in their correct position.
            PrintOrderDetails("Gift Shop", 31, productName: "Red Mug");
            PrintOrderDetails(sellerName: "Gift Shop", 31, productName: "Red Mug");    // C# 7.2 onwards
            PrintOrderDetails("Gift Shop", orderNum: 31, "Red Mug");                   // C# 7.2 onwards

            // However, mixed arguments are invalid if used out-of-order.
            // The following statements will cause a compiler error.
            // PrintOrderDetails(productName: "Red Mug", 31, "Gift Shop");
            // PrintOrderDetails(31, sellerName: "Gift Shop", "Red Mug");
            // PrintOrderDetails(31, "Red Mug", sellerName: "Gift Shop");
        }

        static void PrintOrderDetails(string sellerName, int orderNum, string productName) {
            if (string.IsNullOrWhiteSpace(sellerName)) {
                throw new ArgumentException(message: "Seller name cannot be null or empty.", paramName: nameof(sellerName));
            }

            Console.WriteLine($"Seller: {sellerName}, Order #: {orderNum}, Product: {productName}");
        }
    }
    #endregion

    #region Private protected
    public class BaseClass
    {
        private protected int myValue = 0;
    }

    public class DerivedClass1 : BaseClass
    {
        void Access() {
            var baseObject = new BaseClass();

            // Error CS1540, because myValue can only be accessed by
            // classes derived from BaseClass.
            // baseObject.myValue = 5;

            // OK, accessed through the current derived class instance
            myValue = 5;
        }
    }
    #endregion
    class Others
	{
        void M() {
            int binaryValue = 0b_0101_0101;
            var arr = new int[0];
            var otherArr = arr;
            ref var r = ref (arr != null ? ref arr[0] : ref otherArr[0]);
        }
	}
}
