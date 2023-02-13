using Liella.Compiler;
using Liella.Compiler.LLVM;
using Liella.Image;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Liella.MSIL {
    public class MethodBasicBlock {
        public MethodBasicBlock[] TrueExit { get; set; }
        public MethodBasicBlock FalseExit { get; set; }
        protected ImageMethodInstance m_MethodInfo;
        protected Interval m_Interval;
        protected List<MethodBasicBlock> m_Predecessor = new List<MethodBasicBlock>();
        public int StackDepthDelta { get; set; }
        public uint StartIndex => m_Interval.Left;
        public uint EndIndex => m_Interval.Right;
        public Interval Interval => m_Interval;
        public ImageMethodInstance Method => m_MethodInfo;

        public int ExitStackDepth { get; set; } = int.MinValue;

        public LLVMCompValue[] PreStack { get => m_PreStack; set => m_PreStack = value; }
        public List<MethodBasicBlock> Predecessor => m_Predecessor;

        protected LLVMCompValue[] m_PreStack;
        public MethodBasicBlock(ImageMethodInstance method, uint startIndex, uint endIndex) {
            m_MethodInfo = method;
            m_Interval = new Interval(startIndex, endIndex);
        }
        public MethodBasicBlock Split(uint pos) {
            if (m_Interval.Left == pos) return null;
            var newBlock = new MethodBasicBlock(m_MethodInfo, pos, m_Interval.Right);
            m_Interval.Right = pos;
            newBlock.TrueExit = TrueExit;
            newBlock.FalseExit = FalseExit;
            FalseExit = newBlock;
            return newBlock;
        }
        public static MethodBasicBlock CutBasicBlock(uint cutIndex, SortedList<Interval, MethodBasicBlock> basicBlocks) {
            var falseExitCutPoint = new Interval(cutIndex, cutIndex);
            if (basicBlocks.TryGetValue(falseExitCutPoint, out var block0)) {
                var block1 = block0.Split(cutIndex);
                if (block1 != null) {
                    basicBlocks.Remove(falseExitCutPoint);
                    basicBlocks.Add(block0.Interval, block0);
                    basicBlocks.Add(block1.Interval, block1);
                }
                return basicBlocks[falseExitCutPoint];
            } else {
                return null;
            }
        }
        public override string ToString() {
            return $"{m_Interval}->({((TrueExit != null) ? (string.Join(',', TrueExit.Select(e => e.StartIndex).ToArray())) : "")},{FalseExit?.StartIndex})";
        }

    }

}
