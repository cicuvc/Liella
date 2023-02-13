using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.MSIL {
    public struct Interval {
        public uint Left { get; set; }
        public uint Right { get; set; }
        public class IntervalTreeComparer : IComparer<Interval> {
            public int Compare(Interval x, Interval y) {
                if (x.Left == x.Right) { var t = x; x = y; y = t; }
                var xl = x.Left;
                var xr = x.Right;
                var yv = y.Left;
                return (xl > yv ? 1 : (xr > yv ? 0 : -1));
            }
        }
        public Interval(uint left, uint right) {
            Left = left;
            Right = right;
        }
        public override string ToString() {
            return $"[{Left},{Right})";
        }
    }
}
