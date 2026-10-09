using System;

namespace Elevator.Reporting.Figures
{
    public struct FigurePoint
    {
        public double X;
        public double Y;

        public FigurePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static FigurePoint operator +(FigurePoint a, FigurePoint b)
        {
            return new FigurePoint(a.X + b.X, a.Y + b.Y);
        }

        public static FigurePoint operator -(FigurePoint a, FigurePoint b)
        {
            return new FigurePoint(a.X - b.X, a.Y - b.Y);
        }

        public static FigurePoint operator *(FigurePoint a, double s)
        {
            return new FigurePoint(a.X * s, a.Y * s);
        }

        public double Length
        {
            get
            {
                return Math.Sqrt(X * X + Y * Y);
            }
        }

        public FigurePoint Unit
        {
            get
            {
                double l = Length;
                return l > 0 ? new FigurePoint(X / l, Y / l) : new FigurePoint(0, 0);
            }
        }

        public FigurePoint Normal
        {
            get
            {
                return new FigurePoint(-Y, X);
            }
        }
    }
}
